using KnowledgeSearch.Core.Domain.Search;
using KnowledgeSearch.Core.Domain.Sources;
using Microsoft.Data.Sqlite;

namespace KnowledgeSearch;

internal sealed class DbService : IDbService, IDisposable
{
    private const int _schemaVersion = 3;

    private static readonly IReadOnlyList<string> DefaultExcludedDirectoryNames =
    [
        "node_modules", ".git", "bin", "obj",
        "dist", "build", "out", "coverage", "TestResults",
        ".next", ".nuxt", ".turbo", ".cache", ".vite", ".svelte-kit",
        ".pnpm-store", "storybook-static", ".vs", ".idea",
    ];

    private static readonly IReadOnlyList<string> MarkdownExtensions = [".md", ".mkd"];
    private readonly RecursiveDirectoryWalker _walker = new();
    private Dictionary<string, IReadOnlyList<string>> _excludesByRoot;

    private const string _insertSql =
        "INSERT INTO docs(title,section,content,path,line) " +
        "VALUES(@title,@section,@content,@path,@line)";

    private const string _searchSql =
        "SELECT title,section,path,line,content " +
        "FROM docs WHERE docs MATCH @query " +
        "ORDER BY bm25(docs,10,5,1) LIMIT @limit";

    private const string _getStoredModifiedAtSql =
        "SELECT last_modified FROM docs_meta WHERE path=@path";

    private const string _upsertMetaSql =
        "INSERT INTO docs_meta(path,last_modified) VALUES(@path,@modifiedAt) " +
        "ON CONFLICT(path) DO UPDATE SET last_modified=excluded.last_modified";

    private readonly SqliteConnection _connection;
    private IReadOnlyList<string> _roots;
    private Dictionary<string, string> _rootLabels;
    private readonly object _connectionLock = new();

    public DbService(string dbPath, IReadOnlyList<string> roots)
        : this(dbPath, roots.Select(BuildKnowledgeSource).ToList())
    {
    }

    private readonly string _connectionString;

    public DbService(string dbPath, IReadOnlyList<ConfiguredSource> sources)
    {
        var rootPaths = sources.Select(s => s.GetAccessiblePath()).ToList();
        _roots = NormalizeRoots(rootPaths);
        _rootLabels = BuildRootLabels(_roots);
        _excludesByRoot = BuildExcludesByRoot(_roots, sources);
        // Pooling=False en la cadena de read porque las conexiones del pool
        // mantienen archivos abiertos entre tests y rompen la limpieza/recreacion
        // de schema. El coste de abrir/cerrar una conexion SQLite es minimo.
        _connectionString = $"Data Source={dbPath};Pooling=False";
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        ApplyPragmas();
        EnsureSchema();
    }

    private static ConfiguredSource BuildKnowledgeSource(string root) =>
        ConfiguredSource.Create(
            id:       ConfiguredSource.CreateId(root),
            name:     ConfiguredSource.CreateId(root),
            kind:     SourceKind.Knowledge,
            hostPath: root);

    private static Dictionary<string, IReadOnlyList<string>> BuildExcludesByRoot(
        IReadOnlyList<string> normalizedRoots,
        IReadOnlyList<ConfiguredSource> sources)
    {
        var dict = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in normalizedRoots)
        {
            var match = sources.FirstOrDefault(s => string.Equals(
                s.GetAccessiblePath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root,
                StringComparison.OrdinalIgnoreCase));
            dict[root] = match?.Excludes ?? [];
        }
        return dict;
    }

    // ── IDbService ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IReadOnlyList<SearchResult> Search(
        string query,
        int limit,
        SearchMode modes = SearchMode.Default,
        IReadOnlyList<string>? roots = null)
    {
        // Lectura: usamos una conexion fresca (del pool de Microsoft.Data.Sqlite)
        // en lugar de la _connection compartida. Con journal_mode=WAL, los reads
        // pueden ir en paralelo con el writer y no requieren _connectionLock,
        // que es lo que hacia que /search se quedara pending durante indexado.
        var ftsQuery = BuildFtsQuery(query, modes);

        // Snapshot lock-free: _roots y _rootLabels se asignan por completo (no
        // mutan), asi que un read sin lock ve la version vieja O la nueva, no
        // un estado roto. Tomar el lock aqui anularia el beneficio de la
        // conexion separada — bloquearia al lector mientras el writer holdea
        // el lock durante todo el indexado.
        var rootsSnapshot = _roots;
        var rootLabelsSnapshot = _rootLabels;
        var rootFilters = roots is null || roots.Count == 0
            ? []
            : rootsSnapshot
                .Where(root => roots.Contains(rootLabelsSnapshot[root], StringComparer.OrdinalIgnoreCase))
                .ToArray();

        if (roots is not null && roots.Count > 0 && rootFilters.Length == 0)
        {
            return [];
        }

        var sql = _searchSql;
        if (rootFilters.Length > 0)
        {
            var rootPredicates = rootFilters
                .Select((_, index) => $"path LIKE @root{index} ESCAPE '\\'")
                .ToArray();
            sql = _searchSql.Replace(
                "WHERE docs MATCH @query",
                $"WHERE docs MATCH @query AND ({string.Join(" OR ", rootPredicates)})");
        }

        var results = new List<SearchResult>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@query", ftsQuery);
        command.Parameters.AddWithValue("@limit", limit);
        for (var i = 0; i < rootFilters.Length; i++)
        {
            var prefix = rootFilters[i]
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            command.Parameters.AddWithValue($"@root{i}", EscapeLike(prefix) + "%");
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var path = reader.GetString(2);
            results.Add(new SearchResult(
                reader.GetString(0),
                reader.GetString(1),
                path,
                reader.GetInt32(3),
                reader.GetString(4),
                GetRoot(path)));
        }

        return results;
    }

    /// <inheritdoc/>
    public IndexResult IndexDirectories()
    {
        lock (_connectionLock)
        {
            var files = _roots
                .Where(Directory.Exists)
                .SelectMany(root => _walker.Enumerate(root, new DirectoryWalkOptions(
                    IncludeExtensions:      MarkdownExtensions,
                    ExcludedDirectoryNames: DefaultExcludedDirectoryNames,
                    ExcludeGlobs:           _excludesByRoot.GetValueOrDefault(root) ?? [])).Files)
                .Select(Path.GetFullPath)
                .ToHashSet();

            var indexed = new HashSet<string>();
            using (var command = new SqliteCommand("SELECT path FROM docs_meta", _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    indexed.Add(reader.GetString(0));
                }
            }

            int deleted = 0, added = 0, updated = 0;

            var toDelete = indexed.Except(files).ToList();
            if (toDelete.Count > 0)
            {
                using var deleteTransaction = _connection.BeginTransaction();
                try
                {
                    foreach (var removed in toDelete)
                    {
                        using (var docsCmd = new SqliteCommand("DELETE FROM docs WHERE path=@path", _connection, deleteTransaction))
                        {
                            docsCmd.Parameters.AddWithValue("@path", removed);
                            docsCmd.ExecuteNonQuery();
                        }
                        using (var metaCmd = new SqliteCommand("DELETE FROM docs_meta WHERE path=@path", _connection, deleteTransaction))
                        {
                            metaCmd.Parameters.AddWithValue("@path", removed);
                            metaCmd.ExecuteNonQuery();
                        }
                        deleted++;
                    }
                    deleteTransaction.Commit();
                }
                catch
                {
                    deleteTransaction.Rollback();
                    throw;
                }
            }

            foreach (var file in files)
            {
                var modifiedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds();
                long? stored;
                using (var command = new SqliteCommand(_getStoredModifiedAtSql, _connection))
                {
                    command.Parameters.AddWithValue("@path", file);
                    var result = command.ExecuteScalar();
                    stored = result is null or DBNull ? null : Convert.ToInt64(result);
                }

                if (stored == modifiedAt)
                {
                    continue;
                }

                using var transaction = _connection.BeginTransaction();
                try
                {
                    using (var deleteCmd = new SqliteCommand("DELETE FROM docs WHERE path=@path", _connection, transaction))
                    {
                        deleteCmd.Parameters.AddWithValue("@path", file);
                        deleteCmd.ExecuteNonQuery();
                    }

                    IndexFileSections(file, Path.GetFileNameWithoutExtension(file), transaction);

                    using (var metaCmd = new SqliteCommand(_upsertMetaSql, _connection, transaction))
                    {
                        metaCmd.Parameters.AddWithValue("@path", file);
                        metaCmd.Parameters.AddWithValue("@modifiedAt", modifiedAt);
                        metaCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }

                if (stored is null)
                {
                    added++;
                }
                else
                {
                    updated++;
                }
            }

            return new IndexResult(added, updated, deleted);
        }
    }

    /// <inheritdoc/>
    public bool IsPathAllowed(string fullPath)
    {
        var snapshot = _roots;
        return snapshot.Any(root =>
            fullPath.StartsWith(
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public void ReindexFile(string path)
    {
        lock (_connectionLock)
        {
            if (!File.Exists(path))
            {
                return;
            }

            var modifiedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();

            using var transaction = _connection.BeginTransaction();
            try
            {
                using (var deleteCmd = new SqliteCommand("DELETE FROM docs WHERE path=@path", _connection, transaction))
                {
                    deleteCmd.Parameters.AddWithValue("@path", path);
                    deleteCmd.ExecuteNonQuery();
                }

                IndexFileSections(path, Path.GetFileNameWithoutExtension(path), transaction);

                using (var metaCmd = new SqliteCommand(_upsertMetaSql, _connection, transaction))
                {
                    metaCmd.Parameters.AddWithValue("@path", path);
                    metaCmd.Parameters.AddWithValue("@modifiedAt", modifiedAt);
                    metaCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public void DeleteFile(string path)
    {
        lock (_connectionLock)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                using (var docsCmd = new SqliteCommand("DELETE FROM docs WHERE path=@path", _connection, transaction))
                {
                    docsCmd.Parameters.AddWithValue("@path", path);
                    docsCmd.ExecuteNonQuery();
                }

                using (var metaCmd = new SqliteCommand("DELETE FROM docs_meta WHERE path=@path", _connection, transaction))
                {
                    metaCmd.Parameters.AddWithValue("@path", path);
                    metaCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetRootNames()
    {
        var rootsSnapshot = _roots;
        var labelsSnapshot = _rootLabels;
        return rootsSnapshot.Select(root => labelsSnapshot[root]).ToList();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        GC.SuppressFinalize(this);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    internal static string BuildFtsQuery(string query, SearchMode modes = SearchMode.Default)
    {
        var words = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 1)
        {
            return "\"" + words[0].Replace("\"", "\"\"") + "\"";
        }

        var escaped = words.Select(word => word.Replace("\"", "\"\"")).ToArray();
        var parts = new List<string>();

        if (modes.HasFlag(SearchMode.Phrase))
        {
            parts.Add("\"" + string.Join(" ", escaped) + "\"");
        }

        if (modes.HasFlag(SearchMode.And))
        {
            parts.Add("(" + string.Join(" AND ", escaped) + ")");
        }

        if (modes.HasFlag(SearchMode.Or))
        {
            foreach (var word in escaped)
            {
                parts.Add("\"" + word + "\"");
            }
        }

        return parts.Count > 0
            ? string.Join(" OR ", parts)
            : "\"" + string.Join(" ", escaped) + "\"";
    }

    private string GetRoot(string path)
    {
        foreach (var root in _roots)
        {
            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return _rootLabels[root];
            }
        }

        return string.Empty;
    }

    public void UpdateRoots(IReadOnlyList<string> newRoots)
    {
        UpdateSources(newRoots.Select(BuildKnowledgeSource).ToList());
    }

    // NOTA: solo muta el estado interno (roots + excludes). NO re-indexa — el
    // re-indexado se encola como job (IndexDocumentsJob) por quien llama, para
    // no bloquear la peticion HTTP. Vease Program.cs / SaveSourcesUseCase.
    public void UpdateSources(IReadOnlyList<ConfiguredSource> newSources)
    {
        var rootPaths = newSources.Select(s => s.GetAccessiblePath()).ToList();
        var normalized = NormalizeRoots(rootPaths);
        lock (_connectionLock)
        {
            _roots = normalized;
            _rootLabels = BuildRootLabels(normalized);
            _excludesByRoot = BuildExcludesByRoot(normalized, newSources);
        }
    }

    private static string[] NormalizeRoots(IReadOnlyList<string> roots) =>
        roots
            .Select(root => root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static Dictionary<string, string> BuildRootLabels(IReadOnlyList<string> roots)
    {
        var normalizedRoots = roots
            .Select(root => root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .ToArray();

        var labels = normalizedRoots.ToDictionary(root => root, root => Path.GetFileName(root)!);
        var duplicateLabels = labels.Values
            .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (duplicateLabels.Count == 0)
        {
            return labels;
        }

        foreach (var root in normalizedRoots.Where(root => duplicateLabels.Contains(labels[root])))
        {
            var parts = root.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            labels[root] = parts.Length >= 2
                ? string.Join("/", parts.TakeLast(2))
                : root;
        }

        var stillDuplicateLabels = labels.Values
            .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var root in normalizedRoots.Where(root => stillDuplicateLabels.Contains(labels[root])))
        {
            labels[root] = root.Replace(Path.DirectorySeparatorChar, '/');
        }

        return labels;
    }

    private static string EscapeLike(string value) =>
        value
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");

    private void IndexFileSections(string path, string title, SqliteTransaction transaction)
    {
        var lines = File.ReadAllLines(path);
        var buffer = new List<string>();
        var startLine = 1;
        var section = title;

        void Flush()
        {
            if (buffer.Count == 0)
            {
                return;
            }

            if (buffer.All(line => string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')))
            {
                return;
            }

            using var command = new SqliteCommand(_insertSql, _connection, transaction);
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@section", section);
            command.Parameters.AddWithValue("@content", string.Join("\n", buffer).Trim());
            command.Parameters.AddWithValue("@path", path);
            command.Parameters.AddWithValue("@line", startLine);
            command.ExecuteNonQuery();
            buffer.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('#'))
            {
                Flush();
                startLine = i + 1;
                section = lines[i].TrimStart('#').Trim();
            }

            buffer.Add(lines[i]);
        }

        Flush();
    }

    private void ApplyPragmas()
    {
        ExecuteSql("PRAGMA journal_mode=WAL");
        ExecuteSql("PRAGMA cache_size=-32000");
        ExecuteSql("PRAGMA synchronous=NORMAL");
    }

    private void EnsureSchema()
    {
        ExecuteSql("CREATE TABLE IF NOT EXISTS schema_info(version INTEGER NOT NULL)");
        var version = QuerySchemaVersion();

        if (version == _schemaVersion)
        {
            return;
        }

        ExecuteSql("DROP TABLE IF EXISTS docs");
        ExecuteSql("DROP TABLE IF EXISTS docs_meta");
        ExecuteSql("""
            CREATE VIRTUAL TABLE docs USING fts5(
                title, section, content,
                path UNINDEXED, line UNINDEXED,
                tokenize='trigram')
            """);
        ExecuteSql("CREATE TABLE docs_meta(path TEXT PRIMARY KEY, last_modified INTEGER NOT NULL)");

        if (version is null)
        {
            ExecuteSql($"INSERT INTO schema_info(version) VALUES({_schemaVersion})");
        }
        else
        {
            ExecuteSql($"UPDATE schema_info SET version={_schemaVersion}");
        }
    }

    private void ExecuteSql(string sql)
    {
        using var command = new SqliteCommand(sql, _connection);
        command.ExecuteNonQuery();
    }

    private long? QuerySchemaVersion()
    {
        using var command = new SqliteCommand("SELECT version FROM schema_info LIMIT 1", _connection);
        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : Convert.ToInt64(result);
    }
}

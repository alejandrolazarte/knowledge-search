using Microsoft.Data.Sqlite;

namespace KnowledgeSearch;

internal sealed class CodeGraphRepository : ICodeGraphRepository
{
    private const string DeleteNodesSql = "DELETE FROM code_nodes WHERE repo_name = @repoName";
    private const string DeleteEdgesSql = "DELETE FROM code_edges WHERE repo_name = @repoName";
    private const string UpsertRepoSql =
        "INSERT INTO code_repos(name, last_scanned) VALUES(@name, @lastScanned) " +
        "ON CONFLICT(name) DO UPDATE SET last_scanned = excluded.last_scanned";
    private const string InsertNodeSql =
        "INSERT INTO code_nodes(repo_name, identifier, name, kind, file_path, line) " +
        "VALUES(@repoName, @identifier, @name, @kind, @filePath, @line)";
    private const string InsertEdgeSql =
        "INSERT INTO code_edges(repo_name, source_identifier, target_identifier, kind, line) " +
        "VALUES(@repoName, @sourceIdentifier, @targetIdentifier, @kind, @line)";
    private const string SelectNodesSql =
        "SELECT identifier, name, kind, file_path, line FROM code_nodes WHERE repo_name = @repoName";
    private const string SelectEdgesSql =
        "SELECT source_identifier, target_identifier, kind, line FROM code_edges WHERE repo_name = @repoName";
    private const string RepositoryExistsSql =
        "SELECT COUNT(1) FROM code_repos WHERE name = @name";
    private const string SelectRepoNamesSql =
        "SELECT name FROM code_repos ORDER BY name";
    private const string SearchNodesSql =
        "SELECT identifier, name, kind, file_path, line FROM code_nodes " +
        "WHERE repo_name = @repoName AND LOWER(name) LIKE LOWER(@query)";
    private const string DeleteCrossRepoEdgesSql = "DELETE FROM cross_repo_edges";
    private const string InsertCrossRepoEdgeSql =
        "INSERT INTO cross_repo_edges(source_repo, source_identifier, target_repo, target_identifier, kind) " +
        "VALUES(@sourceRepo, @sourceIdentifier, @targetRepo, @targetIdentifier, @kind)";
    private const string SelectCrossRepoEdgesSql =
        "SELECT source_repo, source_identifier, target_repo, target_identifier, kind FROM cross_repo_edges";

    private readonly SqliteConnection _connection;

    public CodeGraphRepository(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        ApplyPragmas();
        EnsureSchema();
    }

    public void SaveScanResult(string repositoryName, CodeGraphScanResult scanResult)
    {
        using var transaction = _connection.BeginTransaction();
        try
        {
            DeleteExistingData(repositoryName, transaction);
            UpsertRepository(repositoryName, transaction);
            InsertNodes(repositoryName, scanResult.Nodes, transaction);
            InsertEdges(repositoryName, scanResult.Edges, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public bool RepositoryExists(string repositoryName)
    {
        using var command = new SqliteCommand(RepositoryExistsSql, _connection);
        command.Parameters.AddWithValue("@name", repositoryName);
        return Convert.ToInt64(command.ExecuteScalar()!, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    public IReadOnlyList<CodeNode> GetNodes(string repositoryName)
    {
        var nodes = new List<CodeNode>();
        using var command = new SqliteCommand(SelectNodesSql, _connection);
        command.Parameters.AddWithValue("@repoName", repositoryName);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            nodes.Add(new CodeNode(
                reader.GetString(0),
                reader.GetString(1),
                Enum.Parse<CodeNodeKind>(reader.GetString(2)),
                reader.GetString(3),
                reader.GetInt32(4)));
        }

        return nodes;
    }

    public IReadOnlyList<CodeEdge> GetEdges(string repositoryName)
    {
        var edges = new List<CodeEdge>();
        using var command = new SqliteCommand(SelectEdgesSql, _connection);
        command.Parameters.AddWithValue("@repoName", repositoryName);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            edges.Add(new CodeEdge(
                reader.GetString(0),
                reader.GetString(1),
                Enum.Parse<CodeEdgeKind>(reader.GetString(2)),
                reader.GetInt32(3)));
        }

        return edges;
    }

    public IReadOnlyList<CodeNode> SearchNodes(string repositoryName, string query)
    {
        var nodes = new List<CodeNode>();
        using var command = new SqliteCommand(SearchNodesSql, _connection);
        command.Parameters.AddWithValue("@repoName", repositoryName);
        command.Parameters.AddWithValue("@query", $"%{query}%");
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            nodes.Add(new CodeNode(
                reader.GetString(0),
                reader.GetString(1),
                Enum.Parse<CodeNodeKind>(reader.GetString(2)),
                reader.GetString(3),
                reader.GetInt32(4)));
        }

        return nodes;
    }

    public IReadOnlyList<string> GetRepositoryNames()
    {
        var names = new List<string>();
        using var command = new SqliteCommand(SelectRepoNamesSql, _connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    public void SaveCrossRepoEdges(IReadOnlyList<CrossRepoCodeEdge> edges)
    {
        using var transaction = _connection.BeginTransaction();
        try
        {
            using var deleteCommand = new SqliteCommand(DeleteCrossRepoEdgesSql, _connection, transaction);
            deleteCommand.ExecuteNonQuery();

            foreach (var edge in edges)
            {
                using var insertCommand = new SqliteCommand(InsertCrossRepoEdgeSql, _connection, transaction);
                insertCommand.Parameters.AddWithValue("@sourceRepo", edge.SourceRepositoryName);
                insertCommand.Parameters.AddWithValue("@sourceIdentifier", edge.SourceIdentifier);
                insertCommand.Parameters.AddWithValue("@targetRepo", edge.TargetRepositoryName);
                insertCommand.Parameters.AddWithValue("@targetIdentifier", edge.TargetIdentifier);
                insertCommand.Parameters.AddWithValue("@kind", edge.Kind.ToString());
                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public IReadOnlyList<CrossRepoCodeEdge> GetCrossRepoEdges()
    {
        var edges = new List<CrossRepoCodeEdge>();
        using var command = new SqliteCommand(SelectCrossRepoEdgesSql, _connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            edges.Add(new CrossRepoCodeEdge(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                Enum.Parse<CrossRepoEdgeKind>(reader.GetString(4))));
        }

        return edges;
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        GC.SuppressFinalize(this);
    }

    private void DeleteExistingData(string repositoryName, SqliteTransaction transaction)
    {
        using var deleteNodes = new SqliteCommand(DeleteNodesSql, _connection, transaction);
        deleteNodes.Parameters.AddWithValue("@repoName", repositoryName);
        deleteNodes.ExecuteNonQuery();

        using var deleteEdges = new SqliteCommand(DeleteEdgesSql, _connection, transaction);
        deleteEdges.Parameters.AddWithValue("@repoName", repositoryName);
        deleteEdges.ExecuteNonQuery();
    }

    private void UpsertRepository(string repositoryName, SqliteTransaction transaction)
    {
        using var command = new SqliteCommand(UpsertRepoSql, _connection, transaction);
        command.Parameters.AddWithValue("@name", repositoryName);
        command.Parameters.AddWithValue("@lastScanned", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        command.ExecuteNonQuery();
    }

    private void InsertNodes(string repositoryName, IReadOnlyList<CodeNode> nodes, SqliteTransaction transaction)
    {
        var uniqueNodes = nodes
            .GroupBy(n => n.Identifier, StringComparer.Ordinal)
            .Select(g => g.First());

        foreach (var node in uniqueNodes)
        {
            using var command = new SqliteCommand(InsertNodeSql, _connection, transaction);
            command.Parameters.AddWithValue("@repoName", repositoryName);
            command.Parameters.AddWithValue("@identifier", node.Identifier);
            command.Parameters.AddWithValue("@name", node.Name);
            command.Parameters.AddWithValue("@kind", node.Kind.ToString());
            command.Parameters.AddWithValue("@filePath", node.FilePath);
            command.Parameters.AddWithValue("@line", node.Line);
            command.ExecuteNonQuery();
        }
    }

    private void InsertEdges(string repositoryName, IReadOnlyList<CodeEdge> edges, SqliteTransaction transaction)
    {
        foreach (var edge in edges)
        {
            using var command = new SqliteCommand(InsertEdgeSql, _connection, transaction);
            command.Parameters.AddWithValue("@repoName", repositoryName);
            command.Parameters.AddWithValue("@sourceIdentifier", edge.SourceIdentifier);
            command.Parameters.AddWithValue("@targetIdentifier", edge.TargetIdentifier);
            command.Parameters.AddWithValue("@kind", edge.Kind.ToString());
            command.Parameters.AddWithValue("@line", edge.Line);
            command.ExecuteNonQuery();
        }
    }

    private void ApplyPragmas()
    {
        ExecuteSql("PRAGMA journal_mode=WAL");
        ExecuteSql("PRAGMA cache_size=-8000");
        ExecuteSql("PRAGMA synchronous=NORMAL");
    }

    private void EnsureSchema()
    {
        ExecuteSql("""
            CREATE TABLE IF NOT EXISTS code_repos (
                name TEXT PRIMARY KEY,
                last_scanned INTEGER NOT NULL
            )
            """);
        ExecuteSql("""
            CREATE TABLE IF NOT EXISTS code_nodes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                repo_name TEXT NOT NULL,
                identifier TEXT NOT NULL,
                name TEXT NOT NULL,
                kind TEXT NOT NULL,
                file_path TEXT NOT NULL,
                line INTEGER NOT NULL,
                FOREIGN KEY(repo_name) REFERENCES code_repos(name)
            )
            """);
        ExecuteSql("""
            CREATE TABLE IF NOT EXISTS code_edges (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                repo_name TEXT NOT NULL,
                source_identifier TEXT NOT NULL,
                target_identifier TEXT NOT NULL,
                kind TEXT NOT NULL,
                line INTEGER NOT NULL,
                FOREIGN KEY(repo_name) REFERENCES code_repos(name)
            )
            """);
        ExecuteSql("CREATE INDEX IF NOT EXISTS idx_code_nodes_repo ON code_nodes(repo_name)");
        ExecuteSql("CREATE INDEX IF NOT EXISTS idx_code_edges_repo ON code_edges(repo_name)");
        ExecuteSql("""
            CREATE TABLE IF NOT EXISTS cross_repo_edges (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_repo TEXT NOT NULL,
                source_identifier TEXT NOT NULL,
                target_repo TEXT NOT NULL,
                target_identifier TEXT NOT NULL,
                kind TEXT NOT NULL
            )
            """);
    }

    private void ExecuteSql(string sql)
    {
        using var command = new SqliteCommand(sql, _connection);
        command.ExecuteNonQuery();
    }
}

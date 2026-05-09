# Search Improvements + Multiple Roots — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor `DbService` to a DI singleton, add cascade FTS query, support multiple documentation roots, index with transactions, add global exception middleware, and replace `AppConfig` with `IConfiguration`.

**Architecture:** `DbService` becomes an `internal sealed class` implementing `IDbService`, registered as a singleton. `SearchEndpoints` resolves `IDbService` from DI instead of owning a connection. `WatcherService` receives `IReadOnlyList<string> roots` and `IDbService`; it creates one `FileSystemWatcher` per root. Global exception middleware replaces all try/catch in services.

**Tech Stack:** .NET 10 minimal API, Microsoft.Data.Sqlite (FTS5 trigram), xUnit + Moq + Shouldly, React 19 + TypeScript, Tailwind CSS.

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `src/Api/Models/Models.cs` | Modify | Add `Root` to `SearchResult`, add `SearchMode` flags enum, add `ErrorResult`, update `AppJsonContext` |
| `src/Api/Persistence/IDbService.cs` | Create | Interface: Search, IndexDirectories, IsPathAllowed, ReindexFile, DeleteFile, GetRootNames |
| `src/Api/Persistence/DbService.cs` | Rewrite | Instance class: connection singleton, BuildFtsQuery, transactions, all IDbService methods |
| `src/Api/Program.cs` | Rewrite | IConfiguration, DI registration, global exception middleware |
| `src/Api/appsettings.json` | Modify | Rename `KnowledgeDir` → `KnowledgeDirs` |
| `src/Api/AppConfig.cs` | Delete | Replaced by IConfiguration in Program.cs |
| `src/Api/Endpoints/SearchEndpoints.cs` | Rewrite | Use IDbService from DI, add `modes`/`roots` params, add `/roots` endpoint |
| `src/Api/Services/WatcherService.cs` | Rewrite | Multiple roots, use IDbService |
| `test/Api.Tests/When_DbServiceSearches.cs` | Create | Tests: phrase search, IsPathAllowed, IndexDirectories with nonexistent root |
| `test/Api.Tests/When_WatcherServiceDetectsChange.cs` | Modify | Mock IDbService, remove DbService.Open from constructor |
| `test/Api.Tests/When_LogEndpointIsRequested.cs` | Modify | Mock IDbService in WebApplicationFactory |
| `app/src/types.ts` | Modify | Add `root: string` to `SearchResult` |
| `app/src/components/SearchView.tsx` | Modify | Root badge, mode chips (localStorage), roots selector |

---

## Task 1: Update Models

**Files:**
- Modify: `src/Api/Models/Models.cs`

- [ ] **Step 1: Replace the contents of `Models.cs`**

```csharp
using System.Text.Json.Serialization;

namespace KnowledgeSearch;

internal record SearchResult(string Title, string Section, string Path, int Line, string Content, string Root);
internal record IndexResult(int Added, int Updated, int Deleted);
internal record HealthResult(string Status);
internal record ErrorResult(string Error);
internal record SkillSummary(string Name, string Description, string DirName);

[Flags]
internal enum SearchMode
{
    None    = 0,
    Phrase  = 1,
    And     = 2,
    Or      = 4,
    Default = Phrase | And | Or,
}

public record LogEvent(
    [property: JsonPropertyName("ts")]   string Ts,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("path")] string Path);

[JsonSerializable(typeof(List<SearchResult>))]
[JsonSerializable(typeof(List<SkillSummary>))]
[JsonSerializable(typeof(List<LogEvent>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(LogEvent))]
[JsonSerializable(typeof(IndexResult))]
[JsonSerializable(typeof(HealthResult))]
[JsonSerializable(typeof(ErrorResult))]
[JsonSerializable(typeof(string))]
internal partial class AppJsonContext : JsonSerializerContext { }
```

- [ ] **Step 2: Build to confirm no errors**

```
dotnet build src/Api/Api.csproj
```

Expected: Build succeeds. `SearchResult` now has 6 positional parameters — the only existing callers are in `SearchEndpoints.cs` (which will be rewritten in Task 6) and the JSON context.

- [ ] **Step 3: Commit**

```bash
git add src/Api/Models/Models.cs
git commit -m "feat: add Root to SearchResult, add SearchMode flags enum and ErrorResult"
```

---

## Task 2: Create IDbService Interface

**Files:**
- Create: `src/Api/Persistence/IDbService.cs`

- [ ] **Step 1: Create the file**

```csharp
namespace KnowledgeSearch;

internal interface IDbService : IDisposable
{
    /// <summary>
    /// Busca documentos usando cascade phrase → AND → OR según <paramref name="modes"/>.
    /// Si <paramref name="roots"/> está vacío busca en todos los roots configurados.
    /// Retorna hasta <paramref name="limit"/> resultados ordenados por BM25.
    /// </summary>
    IReadOnlyList<SearchResult> Search(
        string query,
        int limit,
        SearchMode modes = SearchMode.Default,
        IReadOnlyList<string>? roots = null);

    /// <summary>
    /// Re-indexa todos los roots de forma incremental (solo archivos modificados).
    /// Usa una transacción por archivo para mayor performance.
    /// </summary>
    IndexResult IndexDirectories();

    /// <summary>
    /// Valida que el path esté dentro de alguno de los roots configurados.
    /// Usado por /file e /image para prevenir path traversal.
    /// </summary>
    bool IsPathAllowed(string fullPath);

    /// <summary>
    /// Re-indexa un archivo específico. Usado por WatcherService en cambios incrementales.
    /// </summary>
    void ReindexFile(string path);

    /// <summary>
    /// Elimina un archivo del índice. Usado por WatcherService cuando se detecta una eliminación.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Retorna los nombres (último segmento del path) de los roots configurados.
    /// </summary>
    IReadOnlyList<string> GetRootNames();
}
```

- [ ] **Step 2: Build**

```
dotnet build src/Api/Api.csproj
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Api/Persistence/IDbService.cs
git commit -m "feat: add IDbService interface"
```

---

## Task 3: Rewrite DbService as Instance Class

**Files:**
- Rewrite: `src/Api/Persistence/DbService.cs`

This replaces the entire static class with an instance that implements `IDbService`. The connection is opened once in the constructor and reused for all operations.

- [ ] **Step 1: Replace the entire contents of `DbService.cs`**

```csharp
using Microsoft.Data.Sqlite;

namespace KnowledgeSearch;

internal sealed class DbService : IDbService
{
    private const int SchemaVersion = 3;

    private const string InsertSql =
        "INSERT INTO docs(title,section,content,path,line) " +
        "VALUES(@title,@section,@content,@path,@line)";

    private const string SearchSql =
        "SELECT title,section,path,line,content " +
        "FROM docs WHERE docs MATCH @query " +
        "ORDER BY bm25(docs,10,5,1) LIMIT @limit";

    private const string GetStoredModifiedAtSql =
        "SELECT last_modified FROM docs_meta WHERE path=@path";

    private const string UpsertMetaSql =
        "INSERT INTO docs_meta(path,last_modified) VALUES(@path,@modifiedAt) " +
        "ON CONFLICT(path) DO UPDATE SET last_modified=excluded.last_modified";

    private readonly SqliteConnection _connection;
    private readonly IReadOnlyList<string> _roots;

    public DbService(string dbPath, IReadOnlyList<string> roots)
    {
        _roots = roots;
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        ApplyPragmas();
        EnsureSchema();
    }

    // ── IDbService ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IReadOnlyList<SearchResult> Search(
        string query,
        int limit,
        SearchMode modes = SearchMode.Default,
        IReadOnlyList<string>? roots = null)
    {
        var ftsQuery = BuildFtsQuery(query, modes);
        var rawResults = new List<SearchResult>();

        using var command = new SqliteCommand(SearchSql, _connection);
        command.Parameters.AddWithValue("@query", ftsQuery);
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var path = reader.GetString(2);
            rawResults.Add(new SearchResult(
                reader.GetString(0),
                reader.GetString(1),
                path,
                reader.GetInt32(3),
                reader.GetString(4),
                GetRoot(path)));
        }

        if (roots is null || roots.Count == 0)
        {
            return rawResults;
        }

        return rawResults
            .Where(result => roots.Contains(result.Root, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <inheritdoc/>
    public IndexResult IndexDirectories()
    {
        var separator = Path.DirectorySeparatorChar;

        var files = _roots
            .Where(Directory.Exists)
            .SelectMany(root =>
                Directory.EnumerateFiles(root, "*.md",  SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(root, "*.mkd", SearchOption.AllDirectories)))
            .Select(Path.GetFullPath)
            .Where(filePath =>
                !filePath.Contains($"{separator}node_modules{separator}") &&
                !filePath.Contains($"{separator}.git{separator}"))
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

        foreach (var file in files)
        {
            var modifiedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds();
            long? stored;
            using (var command = new SqliteCommand(GetStoredModifiedAtSql, _connection))
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
            using (var deleteCmd = new SqliteCommand("DELETE FROM docs WHERE path=@path", _connection, transaction))
            {
                deleteCmd.Parameters.AddWithValue("@path", file);
                deleteCmd.ExecuteNonQuery();
            }

            IndexFileSections(file, Path.GetFileNameWithoutExtension(file), transaction);

            using (var metaCmd = new SqliteCommand(UpsertMetaSql, _connection, transaction))
            {
                metaCmd.Parameters.AddWithValue("@path", file);
                metaCmd.Parameters.AddWithValue("@modifiedAt", modifiedAt);
                metaCmd.ExecuteNonQuery();
            }

            transaction.Commit();

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

    /// <inheritdoc/>
    public bool IsPathAllowed(string fullPath) =>
        _roots.Any(root =>
            fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc/>
    public void ReindexFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var modifiedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();

        using var transaction = _connection.BeginTransaction();

        using (var deleteCmd = new SqliteCommand("DELETE FROM docs WHERE path=@path", _connection, transaction))
        {
            deleteCmd.Parameters.AddWithValue("@path", path);
            deleteCmd.ExecuteNonQuery();
        }

        IndexFileSections(path, Path.GetFileNameWithoutExtension(path), transaction);

        using (var metaCmd = new SqliteCommand(UpsertMetaSql, _connection, transaction))
        {
            metaCmd.Parameters.AddWithValue("@path", path);
            metaCmd.Parameters.AddWithValue("@modifiedAt", modifiedAt);
            metaCmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <inheritdoc/>
    public void DeleteFile(string path)
    {
        using var transaction = _connection.BeginTransaction();

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

    /// <inheritdoc/>
    public IReadOnlyList<string> GetRootNames() =>
        _roots.Select(root => Path.GetFileName(root)!).ToList();

    /// <inheritdoc/>
    public void Dispose() => _connection.Dispose();

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
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileName(root)!;
            }
        }

        return string.Empty;
    }

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

            using var command = new SqliteCommand(InsertSql, _connection, transaction);
            command.Parameters.AddWithValue("@title",   title);
            command.Parameters.AddWithValue("@section", section);
            command.Parameters.AddWithValue("@content", string.Join("\n", buffer).Trim());
            command.Parameters.AddWithValue("@path",    path);
            command.Parameters.AddWithValue("@line",    startLine);
            command.ExecuteNonQuery();
            buffer.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('#'))
            {
                Flush();
                startLine = i + 1;
                section   = lines[i].TrimStart('#').Trim();
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

        if (version == SchemaVersion)
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
            ExecuteSql($"INSERT INTO schema_info(version) VALUES({SchemaVersion})");
        }
        else
        {
            ExecuteSql($"UPDATE schema_info SET version={SchemaVersion}");
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
```

- [ ] **Step 2: Build**

```
dotnet build src/Api/Api.csproj
```

Expected: Errors in `SearchEndpoints.cs`, `WatcherService.cs`, and `Program.cs` because they still reference the old static `DbService`. That is expected — those will be fixed in Tasks 5–7.

- [ ] **Step 3: Commit**

```bash
git add src/Api/Persistence/DbService.cs src/Api/Persistence/IDbService.cs
git commit -m "feat: refactor DbService to instance class with IDbService interface"
```

---

## Task 4: New Test — When_DbServiceSearches

**Files:**
- Create: `test/Api.Tests/When_DbServiceSearches.cs`

- [ ] **Step 1: Create the test file**

```csharp
using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_DbServiceSearches : IDisposable
{
    private readonly string _docsDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _dbPath  = Path.GetTempFileName();
    private readonly DbService _sut;

    public When_DbServiceSearches()
    {
        Directory.CreateDirectory(_docsDir);
        _sut = new DbService(_dbPath, new[] { _docsDir });
    }

    [Fact]
    public void Then_PhraseSearchReturnsMatchingResult()
    {
        var filePath = Path.Combine(_docsDir, "guide.md");
        File.WriteAllText(filePath, "# Getting Started\nThis is a getting started guide.");
        _sut.IndexDirectories();

        var results = _sut.Search("getting started", 5);

        results.ShouldHaveSingleItem();
        results[0].Title.ShouldBe("guide");
        results[0].Root.ShouldBe(Path.GetFileName(_docsDir));
    }

    [Fact]
    public void Then_IsPathAllowedReturnsTrueForPathInsideRoot()
    {
        var path = Path.Combine(_docsDir, "subdir", "file.md");

        _sut.IsPathAllowed(path).ShouldBeTrue();
    }

    [Fact]
    public void Then_IsPathAllowedReturnsFalseForPathOutsideRoot()
    {
        _sut.IsPathAllowed(@"C:\Windows\System32\evil.txt").ShouldBeFalse();
    }

    [Fact]
    public void Then_IndexDirectoriesWithNonExistentRootDoesNotThrow()
    {
        var nonExistentRoot = Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid());
        using var sut = new DbService(_dbPath, new[] { nonExistentRoot });

        var result = sut.IndexDirectories();

        result.Added.ShouldBe(0);
        result.Updated.ShouldBe(0);
        result.Deleted.ShouldBe(0);
    }

    public void Dispose()
    {
        _sut.Dispose();
        Directory.Delete(_docsDir, recursive: true);
        File.Delete(_dbPath);
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 2: Run the new test (expects failures since the project doesn't build yet)**

```
dotnet build test/Api.Tests/Api.Tests.csproj 2>&1 | head -30
```

Expected: Build errors because `SearchEndpoints`, `Program.cs`, `WatcherService` still reference old static `DbService`. The test class itself is syntactically correct. Fix those in Tasks 5–7, then run the tests.

- [ ] **Step 3: Commit**

```bash
git add test/Api.Tests/When_DbServiceSearches.cs
git commit -m "test: add When_DbServiceSearches"
```

---

## Task 5: Program.cs + AppConfig Deletion + Middleware

**Files:**
- Rewrite: `src/Api/Program.cs`
- Modify: `src/Api/appsettings.json`
- Delete: `src/Api/AppConfig.cs`

- [ ] **Step 1: Update `appsettings.json`** — rename `KnowledgeDir` → `KnowledgeDirs`

```json
{
  "KnowledgeDb":   "D:/knowledge-search/knowledge.db",
  "KnowledgeDirs": "D:/Documentation",
  "SkillsDir":     "C:/Users/Alejandro/.claude/skills"
}
```

(To support multiple roots later: `"KnowledgeDirs": "D:/Documentation;D:/work/repos"`)

- [ ] **Step 2: Rewrite `Program.cs`**

```csharp
using KnowledgeSearch;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls("http://localhost:5111");
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

var dbPath = builder.Configuration["KnowledgeDb"]
    ?? Path.GetFullPath("../knowledge.db");

var roots = (builder.Configuration["KnowledgeDirs"] ?? string.Empty)
    .Split(';', StringSplitOptions.RemoveEmptyEntries)
    .Select(Path.GetFullPath)
    .ToArray();

var skillsDir = builder.Configuration["SkillsDir"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills");

var indexHtmlPath = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "index.html"))
    ? Path.Combine(Directory.GetCurrentDirectory(), "index.html")
    : Path.Combine(AppContext.BaseDirectory, "index.html");
var staticDir = Path.GetDirectoryName(indexHtmlPath)!;

var logPath = Path.ChangeExtension(dbPath, ".log");

var dbService = new DbService(dbPath, roots);
builder.Services.AddSingleton<IDbService>(dbService);
builder.Services.AddSingleton<ILogService>(new LogService(logPath));
builder.Services.AddHostedService(serviceProvider =>
    new WatcherService(roots, serviceProvider.GetRequiredService<IDbService>(), serviceProvider.GetRequiredService<ILogService>()));

var app = builder.Build();
var logger = app.Logger;

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    var exception = feature?.Error;

    logger.LogError(exception, "Unhandled exception on {Method} {Path}",
        context.Request.Method, context.Request.Path);

    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/json";

    await context.Response.WriteAsJsonAsync(
        new ErrorResult("Error interno del servidor. Revisá los logs para más detalles."),
        AppJsonContext.Default.ErrorResult);
}));

app.MapStaticRoutes(indexHtmlPath, staticDir);
app.MapSkillsRoutes(skillsDir);
app.MapSearchRoutes();
app.MapEventsRoutes();

app.Run();

public partial class Program { }
```

- [ ] **Step 3: Delete `AppConfig.cs`**

```bash
rm /d/knowledge-search/src/Api/AppConfig.cs
```

- [ ] **Step 4: Build (still expects errors from SearchEndpoints and WatcherService)**

```
dotnet build src/Api/Api.csproj 2>&1 | grep -E "error|Error" | head -20
```

Expected: Errors only in `SearchEndpoints.cs` (old signature, old `DbService` usage) and `WatcherService.cs` (old constructor). Confirm `Program.cs` itself has no errors.

- [ ] **Step 5: Commit**

```bash
git add src/Api/Program.cs src/Api/appsettings.json
git rm src/Api/AppConfig.cs
git commit -m "feat: replace AppConfig with IConfiguration, add DI registration and global exception middleware"
```

---

## Task 6: Rewrite SearchEndpoints

**Files:**
- Rewrite: `src/Api/Endpoints/SearchEndpoints.cs`

All endpoints now receive `IDbService` via minimal-API parameter injection. The `/index` endpoint delegates entirely to `IDbService.IndexDirectories()`. The `/file` and `/image` endpoints use `IsPathAllowed()` for security. The `/search` endpoint accepts optional `modes` and `roots` parameters.

- [ ] **Step 1: Replace the entire contents of `SearchEndpoints.cs`**

```csharp
using Microsoft.Data.Sqlite;

namespace KnowledgeSearch;

internal static class SearchEndpoints
{
    public static void MapSearchRoutes(this WebApplication app)
    {
        app.MapGet("/search", (string q, int limit = 5, string? modes = null, string? roots = null,
            IDbService dbService) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest("Falta parámetro q");
            }

            SearchMode searchModes;
            try
            {
                searchModes = modes is not null
                    ? Enum.Parse<SearchMode>(modes, ignoreCase: true)
                    : SearchMode.Default;
            }
            catch (ArgumentException)
            {
                return Results.BadRequest($"modes inválido: '{modes}'. Valores válidos: phrase, and, or");
            }

            var rootFilter = roots?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            try
            {
                var results = dbService.Search(q, limit, searchModes, rootFilter);
                return Results.Ok(results);
            }
            catch (SqliteException ex)
            {
                return Results.BadRequest($"Query inválida: {ex.Message}");
            }
        });

        app.MapPost("/index", (IDbService dbService) =>
        {
            var result = dbService.IndexDirectories();
            return Results.Ok(result);
        });

        app.MapGet("/file", (string path, IDbService dbService) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.BadRequest("Falta parámetro path");
            }

            var fullPath = Path.GetFullPath(path);

            if (!dbService.IsPathAllowed(fullPath))
            {
                return Results.BadRequest("Ruta fuera de los roots configurados");
            }

            if (!File.Exists(fullPath))
            {
                return Results.NotFound();
            }

            return Results.Text(File.ReadAllText(fullPath), "text/plain; charset=utf-8");
        });

        app.MapGet("/image", (string path, IDbService dbService) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.BadRequest("Falta parámetro path");
            }

            var fullPath = Path.GetFullPath(path);

            if (!dbService.IsPathAllowed(fullPath))
            {
                return Results.BadRequest("Ruta fuera de los roots configurados");
            }

            if (!File.Exists(fullPath))
            {
                return Results.NotFound();
            }

            var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
            {
                ".png"  => "image/png",
                ".jpg"  => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif"  => "image/gif",
                ".svg"  => "image/svg+xml",
                ".webp" => "image/webp",
                _       => "application/octet-stream",
            };

            return Results.File(File.ReadAllBytes(fullPath), contentType);
        });

        app.MapGet("/roots", (IDbService dbService) =>
            Results.Ok(dbService.GetRootNames()));

        app.MapGet("/health", () => Results.Ok(new HealthResult("ok")));
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build src/Api/Api.csproj
```

Expected: Build succeeds. Only remaining errors would be from `WatcherService.cs`.

- [ ] **Step 3: Commit**

```bash
git add src/Api/Endpoints/SearchEndpoints.cs
git commit -m "feat: refactor SearchEndpoints to use IDbService, add modes/roots params and /roots endpoint"
```

---

## Task 7: Rewrite WatcherService for Multiple Roots

**Files:**
- Rewrite: `src/Api/Services/WatcherService.cs`

`WatcherService` now receives `IReadOnlyList<string> roots` and `IDbService`. It creates one `FileSystemWatcher` per root. `ProcessChange` calls `dbService.ReindexFile()`, `ProcessDelete` calls `dbService.DeleteFile()`.

- [ ] **Step 1: Replace the entire contents of `WatcherService.cs`**

```csharp
namespace KnowledgeSearch;

internal sealed class WatcherService(
    IReadOnlyList<string> roots,
    IDbService dbService,
    ILogService log) : BackgroundService
{
    private readonly Dictionary<string, Timer> _debounce = [];
    private readonly object _debounceLock = new();

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        foreach (var root in roots.Where(Directory.Exists))
        {
            try
            {
                var watcher = CreateWatcher(root);
                cancellationToken.Register(() => watcher.Dispose());
            }
            catch (Exception ex)
            {
                log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "error",
                    $"Failed to start watcher for {root}: {ex.Message}"));
            }
        }

        return Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private FileSystemWatcher CreateWatcher(string root)
    {
        var watcher = new FileSystemWatcher(root)
        {
            Filter                = "*",
            IncludeSubdirectories = true,
            EnableRaisingEvents   = true,
            NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };

        watcher.Changed += (_, e) => { if (IsMd(e.FullPath)) { Debounce(e.FullPath, "updated"); } };
        watcher.Created += (_, e) => { if (IsMd(e.FullPath)) { Debounce(e.FullPath, "added"); } };
        watcher.Deleted += (_, e) => { if (IsMd(e.FullPath)) { ProcessDelete(e.FullPath); } };
        watcher.Renamed += (_, e) =>
        {
            if (IsMd(e.OldFullPath))
            {
                ProcessDelete(e.OldFullPath);
            }

            if (IsMd(e.FullPath))
            {
                // Real rename between .md files → "added" at destination.
                // Atomic write (temp → target rename) where source was not .md → "updated".
                var eventType = IsMd(e.OldFullPath) ? "added" : "updated";
                Debounce(e.FullPath, eventType);
            }
        };

        return watcher;
    }

    private static bool IsMd(string path) =>
        path.EndsWith(".md",  StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".mkd", StringComparison.OrdinalIgnoreCase);

    private void Debounce(string path, string eventType)
    {
        lock (_debounceLock)
        {
            if (_debounce.TryGetValue(path, out var existing))
            {
                existing.Dispose();
            }

            _debounce[path] = new Timer(_ => ProcessChange(path, eventType), null, 500, Timeout.Infinite);
        }
    }

    internal void ProcessChange(string path, string eventType)
    {
        try
        {
            dbService.ReindexFile(path);
            var relativePath = GetRelativePath(path);
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), eventType, relativePath));
        }
        catch (Exception ex)
        {
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "error", ex.Message));
        }
    }

    internal void ProcessDelete(string path)
    {
        try
        {
            dbService.DeleteFile(path);
            var relativePath = GetRelativePath(path);
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "deleted", relativePath));
        }
        catch (Exception ex)
        {
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "error", ex.Message));
        }
    }

    private string GetRelativePath(string path)
    {
        foreach (var root in roots)
        {
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(root, path).Replace('\\', '/');
            }
        }

        return path.Replace('\\', '/');
    }
}
```

- [ ] **Step 2: Build — should now fully succeed**

```
dotnet build src/Api/Api.csproj
```

Expected: **Build succeeds with 0 errors.**

- [ ] **Step 3: Commit**

```bash
git add src/Api/Services/WatcherService.cs
git commit -m "feat: refactor WatcherService to support multiple roots and use IDbService"
```

---

## Task 8: Update Existing Tests

**Files:**
- Modify: `test/Api.Tests/When_WatcherServiceDetectsChange.cs`
- Modify: `test/Api.Tests/When_LogEndpointIsRequested.cs`

- [ ] **Step 1: Rewrite `When_WatcherServiceDetectsChange.cs`**

The test no longer opens a real DB. Instead it mocks `IDbService` and verifies `ReindexFile` is called.

```csharp
using KnowledgeSearch;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_WatcherServiceDetectsChange : IDisposable
{
    private readonly string _docsDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly Mock<IDbService> _mockDb = new();
    private readonly Mock<ILogService> _mockLog = new();

    public When_WatcherServiceDetectsChange()
    {
        Directory.CreateDirectory(_docsDir);
    }

    [Fact]
    public void Then_LogsAddedEventWithRelativePathWhenFileIsIndexed()
    {
        var filePath = Path.Combine(_docsDir, "guide.md");
        File.WriteAllText(filePath, "# Title\nSome content");

        var sut = new WatcherService(new[] { _docsDir }, _mockDb.Object, _mockLog.Object);
        sut.ProcessChange(filePath, "added");

        _mockDb.Verify(db => db.ReindexFile(filePath), Times.Once);
        _mockLog.Verify(
            l => l.Append(It.Is<LogEvent>(e => e.Type == "added" && e.Path == "guide.md")),
            Times.Once);
        _mockLog.Invocations.ShouldHaveSingleItem();
    }

    public void Dispose()
    {
        Directory.Delete(_docsDir, recursive: true);
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 2: Update `When_LogEndpointIsRequested.cs`** — add `IDbService` mock to `WithWebHostBuilder`

Replace the constructor block:

```csharp
using System.Net;
using System.Net.Http.Json;
using KnowledgeSearch;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_LogEndpointIsRequested : IDisposable
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IDbService>  _mockDb  = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public When_LogEndpointIsRequested()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                var logDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ILogService));
                if (logDescriptor != null)
                {
                    services.Remove(logDescriptor);
                }
                services.AddSingleton(_mockLog.Object);

                var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbService));
                if (dbDescriptor != null)
                {
                    services.Remove(dbDescriptor);
                }
                services.AddSingleton(_mockDb.Object);
            }));
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Then_ReturnsEventsFromLogService()
    {
        var events = new List<LogEvent>
        {
            new(DateTime.UtcNow.ToString("o"), "updated", "golang/install.md")
        };
        _mockLog.Setup(l => l.ReadLast(100)).Returns(events);

        var response = await _client.GetAsync("/log");
        var result   = await response.Content.ReadFromJsonAsync<List<LogEvent>>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.ShouldHaveSingleItem();
        result![0].Type.ShouldBe("updated");
        result![0].Path.ShouldBe("golang/install.md");
        _mockLog.Verify(l => l.ReadLast(100), Times.Once);
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 3: Run all tests**

```
dotnet test test/Api.Tests/Api.Tests.csproj --logger "console;verbosity=normal"
```

Expected: All tests pass, including the new `When_DbServiceSearches` tests.

- [ ] **Step 4: Commit**

```bash
git add test/Api.Tests/When_WatcherServiceDetectsChange.cs test/Api.Tests/When_LogEndpointIsRequested.cs
git commit -m "test: update existing tests to use mocked IDbService"
```

---

## Task 9: Frontend — Root Badge, Mode Chips, Roots Selector

**Files:**
- Modify: `app/src/types.ts`
- Modify: `app/src/components/SearchView.tsx`

### 9a — types.ts

- [ ] **Step 1: Add `root` field to `SearchResult`**

In `app/src/types.ts`, replace the `SearchResult` interface:

```typescript
export interface SearchResult {
  title:   string
  section: string
  path:    string
  line:    number
  content: string
  root:    string
}
```

### 9b — SearchView.tsx

The changes to `SearchView.tsx` are extensive. Here is the complete rewritten file with three new features:
1. **Root badge** — small monospace chip beside the path showing which root the result came from.
2. **Mode chips** — toggleable `Phrase` / `AND` / `OR` chips, collapsed by default, state persisted in `localStorage`.
3. **Roots selector** — multiselect dropdown, only rendered when `GET /roots` returns more than one root.

- [ ] **Step 2: Replace the entire contents of `app/src/components/SearchView.tsx`**

```tsx
import { useState, useRef, useEffect } from 'react'
import { MarkdownContent } from './MarkdownContent'
import { FileModal } from './FileModal'
import type { SearchResult } from '../types'

// ── History ───────────────────────────────────────────────────────────────────
const HISTORY_KEY     = 'ks-search-history'
const MODES_KEY       = 'ks-search-modes'
const ROOTS_KEY       = 'ks-search-roots'
const MAX_HISTORY     = 8

function loadHistory(): string[] {
  try { return JSON.parse(localStorage.getItem(HISTORY_KEY) ?? '[]') } catch { return [] }
}
function pushHistory(q: string, prev: string[]): string[] {
  const next = [q, ...prev.filter(h => h !== q)].slice(0, MAX_HISTORY)
  localStorage.setItem(HISTORY_KEY, JSON.stringify(next))
  return next
}

// ── Search modes ──────────────────────────────────────────────────────────────
type ModeName = 'phrase' | 'and' | 'or'
interface ActiveModes { phrase: boolean; and: boolean; or: boolean }

const DEFAULT_MODES: ActiveModes = { phrase: true, and: true, or: true }

function loadModes(): ActiveModes {
  try { return { ...DEFAULT_MODES, ...JSON.parse(localStorage.getItem(MODES_KEY) ?? '{}') } }
  catch { return DEFAULT_MODES }
}
function saveModes(modes: ActiveModes) {
  localStorage.setItem(MODES_KEY, JSON.stringify(modes))
}

function modesParam(modes: ActiveModes): string {
  const active = (['phrase', 'and', 'or'] as ModeName[]).filter(m => modes[m])
  return active.length === 3 ? '' : active.join(',')  // empty = default (all), no need to send
}

// ── Types ─────────────────────────────────────────────────────────────────────
const LIMITS = [5, 10, 20] as const
type Limit = typeof LIMITS[number]

interface Props {
  statusMsg: string
  onStatus:  (msg: string) => void
  inputRef?: React.RefObject<HTMLInputElement | null>
}

// ── Component ─────────────────────────────────────────────────────────────────
export function SearchView({ statusMsg, onStatus, inputRef: externalRef }: Props) {
  const localRef                              = useRef<HTMLInputElement>(null)
  const inputRef                              = externalRef ?? localRef
  const [query,           setQuery]           = useState('')
  const [searchedQuery,   setSearchedQuery]   = useState('')
  const [results,         setResults]         = useState<SearchResult[]>([])
  const [loading,         setLoading]         = useState(false)
  const [expanded,        setExpanded]        = useState<Set<number>>(new Set())
  const [history,         setHistory]         = useState<string[]>(loadHistory)
  const [showHistory,     setShowHistory]     = useState(false)
  const [limit,           setLimit]           = useState<Limit>(10)
  const [copiedIdx,       setCopiedIdx]       = useState<number | null>(null)
  const [openFile,        setOpenFile]        = useState<string | null>(null)
  const [showModes,       setShowModes]       = useState(false)
  const [activeModes,     setActiveModes]     = useState<ActiveModes>(loadModes)
  const [availableRoots,  setAvailableRoots]  = useState<string[]>([])
  const [selectedRoots,   setSelectedRoots]   = useState<string[]>(() => {
    try { return JSON.parse(localStorage.getItem(ROOTS_KEY) ?? '[]') } catch { return [] }
  })

  // ── Fetch available roots on mount ──────────────────────────────────────────
  useEffect(() => {
    fetch('/roots')
      .then(r => r.json() as Promise<string[]>)
      .then(setAvailableRoots)
      .catch(() => {/* non-critical */})
  }, [])

  // ── Mode chip toggle ────────────────────────────────────────────────────────
  const toggleMode = (mode: ModeName) => {
    setActiveModes(prev => {
      const next = { ...prev, [mode]: !prev[mode] }
      // Prevent deselecting all
      if (!next.phrase && !next.and && !next.or) { return prev }
      saveModes(next)
      return next
    })
  }

  // ── Root toggle ─────────────────────────────────────────────────────────────
  const toggleRoot = (root: string) => {
    setSelectedRoots(prev => {
      const next = prev.includes(root) ? prev.filter(r => r !== root) : [...prev, root]
      localStorage.setItem(ROOTS_KEY, JSON.stringify(next))
      return next
    })
  }

  // ── Search ──────────────────────────────────────────────────────────────────
  const doSearch = async (q = query) => {
    const trimmed = q.trim()
    if (!trimmed) { return }
    setLoading(true)
    setExpanded(new Set())
    setShowHistory(false)
    onStatus('Buscando…')
    try {
      const modesStr = modesParam(activeModes)
      const rootsStr = selectedRoots.join(',')
      const url = `/search?q=${encodeURIComponent(trimmed)}&limit=${limit}` +
        (modesStr ? `&modes=${modesStr}` : '') +
        (rootsStr ? `&roots=${rootsStr}` : '')

      const data: SearchResult[] = await fetch(url).then(r => r.json())
      setResults(data)
      setSearchedQuery(trimmed)
      setHistory(prev => pushHistory(trimmed, prev))
      onStatus(data.length
        ? `${data.length} resultado${data.length !== 1 ? 's' : ''} para "${trimmed}"`
        : `Sin resultados para "${trimmed}"`)
    } catch (e: unknown) {
      onStatus('Error: ' + (e instanceof Error ? e.message : 'desconocido'))
    } finally {
      setLoading(false)
    }
  }

  const clearSearch = () => {
    setQuery('')
    setResults([])
    setSearchedQuery('')
    setShowHistory(false)
    onStatus('')
    inputRef.current?.focus()
  }

  // ── Keyboard ────────────────────────────────────────────────────────────────
  const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter')  { doSearch(); return }
    if (e.key === 'Escape') {
      if (query) { clearSearch() }
      else { setShowHistory(false); inputRef.current?.blur() }
    }
  }

  // ── Expand / collapse ───────────────────────────────────────────────────────
  const toggleExpanded = (i: number) =>
    setExpanded(prev => { const s = new Set(prev); s.has(i) ? s.delete(i) : s.add(i); return s })

  const expandAll   = () => setExpanded(new Set(results.map((_, i) => i)))
  const collapseAll = () => setExpanded(new Set())
  const allExpanded = results.length > 0 && results.every((_, i) => expanded.has(i))

  // ── Copy helpers ──────────────────────────────────────────────────────────
  const copyPath = (r: SearchResult) => {
    const text = `${r.path.replace(/\\/g, '/').split('/').slice(-3).join('/')}:${r.line}`
    navigator.clipboard.writeText(text).then(() => onStatus(`Copiado: ${text}`))
  }

  const copyContent = (r: SearchResult, i: number) => {
    navigator.clipboard.writeText(r.content).then(() => {
      setCopiedIdx(i)
      setTimeout(() => setCopiedIdx(null), 1500)
    })
  }

  const hasSearched = searchedQuery !== ''

  // ── Render ──────────────────────────────────────────────────────────────────
  return (
    <div className="flex-1 flex flex-col overflow-hidden p-4 gap-3">

      {/* ── Search bar ── */}
      <div className="flex gap-2 items-center">
        <div className="relative flex-1">
          <input
            ref={inputRef}
            value={query}
            onChange={e => { setQuery(e.target.value); setShowHistory(true) }}
            onFocus={() => setShowHistory(true)}
            onBlur={() => setTimeout(() => setShowHistory(false), 150)}
            onKeyDown={onKeyDown}
            placeholder="Buscar en knowledge/  ·  Ctrl+K"
            autoFocus
            className="w-full bg-gh-surface border border-gh-border rounded px-3 py-1.5 text-sm text-gh-text
              outline-none focus:border-gh-accent placeholder-gh-muted pr-7"
          />
          {query && (
            <button onClick={clearSearch} tabIndex={-1}
              className="absolute right-2 top-1/2 -translate-y-1/2 text-gh-muted hover:text-gh-text"
            >
              <svg width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          )}

          {/* History dropdown */}
          {showHistory && history.length > 0 && !loading && (
            <div className="absolute top-full left-0 right-0 mt-1 bg-gh-surface border border-gh-border rounded-lg shadow-xl z-10 overflow-hidden">
              <div className="flex items-center justify-between px-3 py-1 border-b border-gh-border">
                <span className="text-[10px] text-gh-muted uppercase tracking-widest">Recientes</span>
                <button
                  onMouseDown={() => { localStorage.removeItem(HISTORY_KEY); setHistory([]) }}
                  className="text-[10px] text-gh-muted hover:text-gh-accent"
                >borrar</button>
              </div>
              {history.slice(0, 6).map((h, i) => (
                <button key={i} onMouseDown={() => { setQuery(h); doSearch(h) }}
                  className="flex items-center gap-2 w-full px-3 py-1.5 text-sm text-gh-muted hover:bg-gh-card hover:text-gh-text text-left"
                >
                  <svg width="11" height="11" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2} className="shrink-0 opacity-50">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 11-18 0 9 9 0 0118 0z"/>
                  </svg>
                  <span className="truncate">{h}</span>
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Limit selector */}
        <div className="flex gap-0.5 shrink-0">
          {LIMITS.map(l => (
            <button key={l} onClick={() => setLimit(l)}
              title={`Máximo ${l} resultados`}
              className={`px-2 py-1.5 text-xs rounded font-medium transition-colors border
                ${limit === l
                  ? 'bg-gh-accent text-white border-transparent'
                  : 'bg-gh-surface text-gh-muted border-gh-border hover:text-gh-text hover:bg-gh-card'}`}
            >{l}</button>
          ))}
        </div>

        <button onClick={() => doSearch()} disabled={loading}
          className="bg-gh-accent hover:opacity-90 disabled:opacity-50 text-white text-sm px-4 py-1.5 rounded font-medium shrink-0"
        >
          Buscar
        </button>
      </div>

      {/* ── Mode chips + Roots selector ── */}
      <div className="flex items-center gap-2 flex-wrap">
        {/* Mode toggle button */}
        <button
          onClick={() => setShowModes(prev => !prev)}
          className="text-[10px] text-gh-muted hover:text-gh-text border border-gh-border rounded px-1.5 py-0.5 transition-colors"
          title="Estrategias de búsqueda FTS"
        >
          {showModes ? '▲ modos' : '▼ modos'}
        </button>

        {showModes && (
          <>
            {(['phrase', 'and', 'or'] as ModeName[]).map(mode => (
              <button
                key={mode}
                onClick={() => toggleMode(mode)}
                title={mode === 'phrase' ? 'Frase exacta' : mode === 'and' ? 'Todas las palabras' : 'Alguna palabra'}
                className={`text-[10px] font-mono px-1.5 py-0.5 rounded border transition-colors
                  ${activeModes[mode]
                    ? 'bg-gh-accent/20 text-gh-accent border-gh-accent/40'
                    : 'bg-gh-surface text-gh-muted border-gh-border opacity-50'}`}
              >
                {mode.toUpperCase()}
              </button>
            ))}
          </>
        )}

        {/* Roots selector — only shown when multiple roots exist */}
        {availableRoots.length > 1 && availableRoots.map(root => (
          <button
            key={root}
            onClick={() => toggleRoot(root)}
            title={`Filtrar por root: ${root}`}
            className={`text-[10px] font-mono px-1.5 py-0.5 rounded border transition-colors
              ${selectedRoots.includes(root)
                ? 'bg-blue-900/30 text-blue-400 border-blue-700/40'
                : 'bg-gh-surface text-gh-muted border-gh-border opacity-50'}`}
          >
            {root}
          </button>
        ))}
      </div>

      {/* ── Status + expand controls ── */}
      <div className="flex items-center justify-between min-h-[16px]">
        {statusMsg && !loading && (
          <p className="text-xs text-gh-muted">{statusMsg}</p>
        )}
        {!loading && results.length > 1 && (
          <button onClick={allExpanded ? collapseAll : expandAll}
            className="ml-auto text-xs text-gh-muted hover:text-gh-accent"
          >
            {allExpanded ? '▲ colapsar todo' : '▼ expandir todo'}
          </button>
        )}
      </div>

      {/* ── Results list ── */}
      <div className="flex-1 overflow-y-auto space-y-2 pr-1">

        {/* Skeleton loading */}
        {loading && [...Array(3)].map((_, i) => (
          <div key={i} className="bg-gh-surface border border-gh-border rounded-lg p-3 animate-pulse">
            <div className="flex items-center justify-between mb-2">
              <div className="h-3.5 bg-gh-card rounded w-40" />
              <div className="h-3 bg-gh-card rounded w-16" />
            </div>
            <div className="h-3 bg-gh-card rounded w-56 mb-3" />
            <div className="space-y-1.5">
              <div className="h-2.5 bg-gh-card rounded w-full" />
              <div className="h-2.5 bg-gh-card rounded w-5/6" />
              <div className="h-2.5 bg-gh-card rounded w-4/6" />
            </div>
          </div>
        ))}

        {/* Initial empty state */}
        {!loading && !hasSearched && (
          <div className="flex flex-col items-center justify-center h-52 gap-3 text-center select-none">
            <svg className="w-10 h-10 text-gh-border" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 15.803 7.5 7.5 0 0015.803 15.803z"/>
            </svg>
            <div>
              <p className="text-gh-muted text-sm">Buscá en el knowledge base</p>
              <p className="text-gh-border text-xs mt-1">
                <kbd className="bg-gh-card border border-gh-border rounded px-1 py-0.5 font-mono text-[10px]">Ctrl+K</kbd>
                {' '}para enfocar  ·
                <kbd className="bg-gh-card border border-gh-border rounded px-1 py-0.5 font-mono text-[10px] ml-1">Enter</kbd>
                {' '}para buscar
              </p>
            </div>
          </div>
        )}

        {/* No results after search */}
        {!loading && hasSearched && results.length === 0 && (
          <div className="flex flex-col items-center justify-center h-52 gap-3 text-center select-none">
            <svg className="w-10 h-10 text-gh-border" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M15.182 16.318A4.486 4.486 0 0012.016 15a4.486 4.486 0 00-3.198 1.318M21 12a9 9 0 11-18 0 9 9 0 0118 0zM9.75 9.75c0 .414-.168.75-.375.75S9 10.164 9 9.75 9.168 9 9.375 9s.375.336.375.75zm-.375 0h.008v.015h-.008V9.75zm5.625 0c0 .414-.168.75-.375.75s-.375-.336-.375-.75.168-.75.375-.75.375.336.375.75zm-.375 0h.008v.015h-.008V9.75z"/>
            </svg>
            <div>
              <p className="text-gh-muted text-sm">Sin resultados para "<span className="text-gh-text">{searchedQuery}</span>"</p>
              <p className="text-gh-border text-xs mt-1">Probá con otros términos</p>
            </div>
          </div>
        )}

        {/* Result cards */}
        {!loading && results.map((r, i) => {
          const isExp   = expanded.has(i)
          const relPath = r.path.replace(/\\/g, '/').split('/').slice(-3).join('/')
          return (
            <div key={i}
              className="bg-gh-surface border border-gh-border rounded-lg p-3 hover:bg-gh-card transition-colors"
            >
              <div className="flex items-center justify-between mb-1">
                <span className="text-sm font-medium text-gh-text truncate pr-2">{r.title}</span>
                <div className="flex items-center gap-1 shrink-0">
                  {/* Ver archivo */}
                  <button onClick={() => setOpenFile(r.path)} title="Ver archivo completo"
                    className="p-1 rounded text-gh-muted hover:text-gh-accent hover:bg-gh-surface transition-colors"
                  >
                    <svg width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M2.036 12.322a1.012 1.012 0 010-.639C3.423 7.51 7.36 4.5 12 4.5c4.638 0 8.573 3.007 9.963 7.178.07.207.07.431 0 .639C20.577 16.49 16.64 19.5 12 19.5c-4.638 0-8.573-3.007-9.963-7.178z"/>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
                    </svg>
                  </button>
                  {/* Copy content */}
                  <button onClick={() => copyContent(r, i)} title="Copiar contenido"
                    className="p-1 rounded text-gh-muted hover:text-gh-accent hover:bg-gh-surface transition-colors"
                  >
                    {copiedIdx === i
                      ? <svg width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5} className="text-green-400">
                          <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5"/>
                        </svg>
                      : <svg width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                          <path strokeLinecap="round" strokeLinejoin="round" d="M15.666 3.888A2.25 2.25 0 0013.5 2.25h-3c-1.03 0-1.9.693-2.166 1.638m7.332 0c.055.194.084.4.084.612v0a.75.75 0 01-.75.75H9a.75.75 0 01-.75-.75v0c0-.212.03-.418.084-.612m7.332 0c.646.049 1.288.11 1.927.184 1.1.128 1.907 1.077 1.907 2.185V19.5a2.25 2.25 0 01-2.25 2.25H6.75A2.25 2.25 0 014.5 19.5V6.257c0-1.108.806-2.057 1.907-2.185a48.208 48.208 0 011.927-.184"/>
                        </svg>
                    }
                  </button>
                  <span className="text-[10px] bg-gh-card text-gh-muted border border-gh-border rounded px-1.5 py-0.5">
                    {r.section}
                  </span>
                </div>
              </div>

              {/* Path + root badge */}
              <div className="flex items-center gap-1 mb-2">
                {r.root && (
                  <span className="text-[10px] font-mono bg-blue-900/30 text-blue-400 border border-blue-800/50 rounded px-1.5 py-0.5 shrink-0">
                    {r.root}
                  </span>
                )}
                <button onClick={() => copyPath(r)}
                  className="text-xs text-gh-accent font-mono hover:underline text-left truncate"
                  title="Click para copiar ruta"
                >
                  {relPath}:{r.line}
                </button>
              </div>

              <div
                className={`overflow-hidden transition-all ${isExp ? '' : 'max-h-28'}`}
                style={{ maskImage: isExp ? 'none' : 'linear-gradient(to bottom, black 60%, transparent 100%)' }}
              >
                <MarkdownContent highlight={searchedQuery} className="prose prose-xs prose-invert max-w-none
                  prose-p:my-0.5 prose-p:text-xs prose-p:text-gh-muted
                  prose-headings:text-gh-text prose-headings:font-semibold prose-headings:my-1
                  prose-h1:text-sm prose-h2:text-sm prose-h3:text-xs
                  prose-pre:bg-transparent prose-pre:p-0 prose-pre:my-1
                  prose-a:text-gh-accent prose-a:no-underline hover:prose-a:underline
                  prose-strong:text-gh-text prose-strong:font-semibold
                  prose-ul:my-0.5 prose-li:my-0 prose-li:text-xs prose-li:text-gh-muted
                  prose-ol:my-0.5
                  prose-table:text-xs prose-th:text-gh-text prose-td:text-gh-muted prose-th:py-0.5 prose-td:py-0.5
                  prose-blockquote:border-gh-border prose-blockquote:text-gh-muted prose-blockquote:text-xs"
                  docPath={r.path}>
                  {r.content}
                </MarkdownContent>
              </div>

              <button onClick={() => toggleExpanded(i)}
                className="mt-1 text-[10px] text-gh-muted hover:text-gh-accent"
              >
                {isExp ? '▲ menos' : '▼ más'}
              </button>
            </div>
          )
        })}
      </div>

      <FileModal path={openFile} onClose={() => setOpenFile(null)} />
    </div>
  )
}
```

- [ ] **Step 3: Build the frontend**

```
cd app && npm run build
```

Expected: Build succeeds with no TypeScript errors.

- [ ] **Step 4: Run all backend tests one final time**

```
dotnet test test/Api.Tests/Api.Tests.csproj --logger "console;verbosity=normal"
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add app/src/types.ts app/src/components/SearchView.tsx
git commit -m "feat: add root badge, mode chips and roots selector to SearchView"
```

---

## Self-Review

**Spec coverage check:**

| Spec requirement | Covered by |
|---|---|
| `DbService` de static a singleton `IDbService` | Tasks 2, 3, 5 |
| `BuildFtsQuery` cascade phrase → AND → OR | Task 3 (`BuildFtsQuery` internal static method) |
| `[Flags]` `SearchMode` enum | Task 1 |
| `modes` param en `/search` | Task 6 |
| `roots` param en `/search` | Task 6 |
| Múltiples roots via `KnowledgeDirs` separados por `;` | Task 5 (Program.cs), Task 3 (IndexDirectories) |
| Campo `Root` en `SearchResult` | Task 1 |
| Transacciones por archivo en indexado | Task 3 (`IndexDirectories`, `ReindexFile`) |
| Middleware global de excepciones | Task 5 |
| `IConfiguration` reemplaza `AppConfig` | Task 5 |
| `WatcherService` múltiples roots | Task 7 |
| XML docs en interface, `/// <inheritdoc/>` en impl | Task 2, 3 |
| Test `When_DbServiceSearches` | Task 4 |
| Test `When_WatcherServiceDetectsChange` actualizado | Task 8 |
| Test `When_LogEndpointIsRequested` actualizado | Task 8 |
| Frontend badge de root | Task 9 |
| Frontend chips de modo | Task 9 |
| Frontend selector de roots | Task 9 |
| `GET /roots` endpoint | Task 6 |
| `IsPathAllowed` en `/file` e `/image` | Task 6 |
| `ErrorResult` record | Task 1 |

**Placeholder scan:** No TBDs, no vague steps, all code blocks complete.

**Type consistency:**
- `SearchResult` has 6 positional params: `Title, Section, Path, Line, Content, Root` — used identically in Task 1 (definition) and Task 3 (construction in `Search()`).
- `IDbService` methods `ReindexFile(string path)` and `DeleteFile(string path)` match calls in Task 7 (`WatcherService`).
- `SearchMode.Default` used in Task 3 signature and Task 6 fallback — consistent.
- `AppJsonContext` updated in Task 1 to include `ErrorResult` and `List<string>` — used in Task 5 middleware and Task 6 `/roots`.

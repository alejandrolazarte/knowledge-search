# API Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganizar la estructura de carpetas, aplicar convenciones de DevHub y limpiar código muerto en el proyecto .NET de knowledge-search.

**Architecture:** Mover archivos a carpetas `Models/`, `Services/`, `Persistence/` (renombrado desde `Db/`). Agregar `.editorconfig` y `Directory.Build.props` al repo. Renombrar helpers abreviados en `DbService` y simplificar `AppConfig`.

**Tech Stack:** .NET 10, ASP.NET Core minimal API, SQLite FTS5, `Microsoft.Data.Sqlite`

**Comandos útiles:**
- Build: `dotnet build D:/knowledge-search/src/Api/Api.csproj`
- Run: `dotnet run --project D:/knowledge-search/src/Api`
- Tests: `dotnet test D:/knowledge-search/test/Api.Tests/Api.Tests.csproj`

---

## Task 1: Limpieza — dead code, archivos temporales y .gitignore

**Files:**
- Delete: `src/Api/marked.min.js`
- Delete: `.api.log`, `.api.err`
- Modify: `.gitignore`

- [ ] Eliminar `marked.min.js`:
```bash
Remove-Item "D:/knowledge-search/src/Api/marked.min.js"
```

- [ ] Eliminar archivos temporales de runtime:
```bash
Remove-Item "D:/knowledge-search/.api.log" -ErrorAction SilentlyContinue
Remove-Item "D:/knowledge-search/.api.err" -ErrorAction SilentlyContinue
```

- [ ] Agregar entradas al `.gitignore` al final del archivo:
```
# Runtime logs
.api.log
.api.err
knowledge.log
```

- [ ] Verificar que el proyecto compila:
```bash
dotnet build D:/knowledge-search/src/Api/Api.csproj
```
Expected: `Build succeeded.`

- [ ] Commit:
```bash
git -C "D:/knowledge-search" add -A
git -C "D:/knowledge-search" commit -m "chore: delete dead code and add runtime files to .gitignore"
```

---

## Task 2: Agregar `.editorconfig` y `Directory.Build.props`

**Files:**
- Create: `.editorconfig` (repo root)
- Create: `Directory.Build.props` (repo root)

- [ ] Crear `D:/knowledge-search/.editorconfig`:
```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

############################
# C# STYLE (GLOBAL)
############################

[*.{cs,csx}]
######## Braces / readability ########
csharp_prefer_braces = true:error
######## Expression-bodied members ########
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = true:suggestion
######## Pattern matching ########
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion
######## Var usage ########
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion
######## Namespaces ########
csharp_style_namespace_declarations = file_scoped:suggestion
######## Primary constructors ########
csharp_style_prefer_primary_constructors = true:suggestion
######## Null & coalescing ########
dotnet_style_null_propagation = true:error
dotnet_style_coalesce_expression = true:error
dotnet_style_prefer_is_null_check_over_reference_equality_method = true:error
dotnet_diagnostic.IDE0041.severity = error
dotnet_diagnostic.IDE0270.severity = error
######## Variables ########
csharp_style_inlined_variable_declaration = true:suggestion
######## Code quality ########
dotnet_diagnostic.IDE0059.severity = warning
dotnet_diagnostic.CA1305.severity = error
######## Formatting ########
dotnet_style_allow_multiple_blank_lines_experimental = false:suggestion
csharp_style_allow_blank_lines_between_consecutive_braces_experimental = false:suggestion
######## Accessibility ########
dotnet_style_require_accessibility_modifiers = always:suggestion
######## Readonly fields ########
dotnet_style_readonly_field = true:suggestion
######## Imports ########
dotnet_separate_import_directive_groups = true:suggestion
dotnet_sort_system_directives_first = true:suggestion
######## Unused parameters ########
dotnet_code_quality_unused_parameters = all:suggestion

############################
# Naming Conventions
############################

dotnet_naming_rule.private_fields_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_underscore.style = underscore_prefix
dotnet_naming_rule.private_fields_underscore.severity = warning
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.underscore_prefix.required_prefix = _
dotnet_naming_style.underscore_prefix.capitalization = camel_case

dotnet_naming_rule.interfaces_start_with_i.symbols = interfaces
dotnet_naming_rule.interfaces_start_with_i.style = interface_style
dotnet_naming_rule.interfaces_start_with_i.severity = warning
dotnet_naming_symbols.interfaces.applicable_kinds = interface
dotnet_naming_style.interface_style.required_prefix = I
dotnet_naming_style.interface_style.capitalization = pascal_case

############################
# TEST FILES
############################

[tests/**/*.cs]
dotnet_diagnostic.CA1707.severity = none

############################
# PROJECT FILES
############################

[*.{csproj,props,targets}]
indent_size = 2

############################
# JSON
############################

[*.json]
indent_size = 2

############################
# MARKDOWN
############################

[*.md]
trim_trailing_whitespace = false
```

- [ ] Crear `D:/knowledge-search/Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Deterministic>true</Deterministic>
    <LangVersion>preview</LangVersion>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] Verificar que el proyecto compila (puede haber nuevas advertencias → errores con `TreatWarningsAsErrors`; arreglarlas antes de continuar):
```bash
dotnet build D:/knowledge-search/src/Api/Api.csproj
```
Expected: `Build succeeded.`

- [ ] Commit:
```bash
git -C "D:/knowledge-search" add .editorconfig Directory.Build.props
git -C "D:/knowledge-search" commit -m "chore: add .editorconfig and Directory.Build.props"
```

---

## Task 3: Slim down `Api.csproj`

**Files:**
- Modify: `src/Api/Api.csproj`
- Modify: `test/Api.Tests/Api.Tests.csproj`

Las propiedades que ya vienen de `Directory.Build.props` se eliminan de los `.csproj` para no duplicarlas.

- [ ] Reemplazar `src/Api/Api.csproj` con:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <RootNamespace>KnowledgeSearch</RootNamespace>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="9.*" />
  </ItemGroup>
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
      <_Parameter1>Api.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
```

- [ ] Reemplazar `test/Api.Tests/Api.Tests.csproj` con:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk"           Version="17.*" />
    <PackageReference Include="xunit"                            Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio"        Version="2.*" />
    <PackageReference Include="Shouldly"                         Version="4.*" />
    <PackageReference Include="Moq"                              Version="4.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Api/Api.csproj" />
  </ItemGroup>
</Project>
```

- [ ] Verificar que compila:
```bash
dotnet build D:/knowledge-search/src/Api/Api.csproj
```
Expected: `Build succeeded.`

- [ ] Commit:
```bash
git -C "D:/knowledge-search" add src/Api/Api.csproj test/Api.Tests/Api.Tests.csproj
git -C "D:/knowledge-search" commit -m "refactor: slim down csproj files (properties moved to Directory.Build.props)"
```

---

## Task 4: Mover archivos a `Models/`, `Services/`, `Persistence/`

**Files:**
- Create: `src/Api/Models/Models.cs` (contenido de `Models.cs`)
- Create: `src/Api/Services/ILogService.cs` (contenido de `ILogService.cs`)
- Create: `src/Api/Services/LogService.cs` (contenido de `LogService.cs`)
- Create: `src/Api/Services/WatcherService.cs` (contenido de `WatcherService.cs`)
- Create: `src/Api/Persistence/DbService.cs` (contenido de `Db/DbService.cs`)
- Delete: `src/Api/Models.cs`
- Delete: `src/Api/ILogService.cs`
- Delete: `src/Api/LogService.cs`
- Delete: `src/Api/WatcherService.cs`
- Delete: `src/Api/Db/DbService.cs` (y la carpeta `Db/`)

Los namespaces NO cambian — todo sigue siendo `namespace KnowledgeSearch;`. Solo cambia la ubicación física de los archivos.

- [ ] Crear carpetas:
```bash
New-Item -ItemType Directory "D:/knowledge-search/src/Api/Models"
New-Item -ItemType Directory "D:/knowledge-search/src/Api/Services"
New-Item -ItemType Directory "D:/knowledge-search/src/Api/Persistence"
```

- [ ] Mover `Models.cs` → `Models/Models.cs`:
```bash
Move-Item "D:/knowledge-search/src/Api/Models.cs" "D:/knowledge-search/src/Api/Models/Models.cs"
```

- [ ] Mover `ILogService.cs` → `Services/ILogService.cs`:
```bash
Move-Item "D:/knowledge-search/src/Api/ILogService.cs" "D:/knowledge-search/src/Api/Services/ILogService.cs"
```

- [ ] Mover `LogService.cs` → `Services/LogService.cs`:
```bash
Move-Item "D:/knowledge-search/src/Api/LogService.cs" "D:/knowledge-search/src/Api/Services/LogService.cs"
```

- [ ] Mover `WatcherService.cs` → `Services/WatcherService.cs`:
```bash
Move-Item "D:/knowledge-search/src/Api/WatcherService.cs" "D:/knowledge-search/src/Api/Services/WatcherService.cs"
```

- [ ] Mover `Db/DbService.cs` → `Persistence/DbService.cs` y eliminar la carpeta `Db/`:
```bash
Move-Item "D:/knowledge-search/src/Api/Db/DbService.cs" "D:/knowledge-search/src/Api/Persistence/DbService.cs"
Remove-Item "D:/knowledge-search/src/Api/Db" -Recurse
```

- [ ] Verificar que compila:
```bash
dotnet build D:/knowledge-search/src/Api/Api.csproj
```
Expected: `Build succeeded.`

- [ ] Commit:
```bash
git -C "D:/knowledge-search" add -A
git -C "D:/knowledge-search" commit -m "refactor: move files to Models/, Services/, Persistence/ folders"
```

---

## Task 5: Agregar access modifiers a todas las clases

**Files:**
- Modify: `src/Api/AppConfig.cs`
- Modify: `src/Api/Models/Models.cs`
- Modify: `src/Api/Services/ILogService.cs`
- Modify: `src/Api/Services/LogService.cs`
- Modify: `src/Api/Services/WatcherService.cs`
- Modify: `src/Api/Persistence/DbService.cs`
- Modify: `src/Api/Endpoints/SearchEndpoints.cs`
- Modify: `src/Api/Endpoints/SkillsEndpoints.cs`
- Modify: `src/Api/Endpoints/StaticEndpoints.cs`
- Modify: `src/Api/Endpoints/EventsEndpoints.cs`

- [ ] Reemplazar `src/Api/AppConfig.cs`:
```csharp
using System.Text.Json;

namespace KnowledgeSearch;

internal static class AppConfig
{
    public static (string DbPath, string DocsDir, string SkillsDir, string IndexHtmlPath, string StaticDir) Load()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(settingsPath))
        {
            settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        }

        JsonElement? settings = File.Exists(settingsPath)
            ? JsonDocument.Parse(File.ReadAllText(settingsPath)).RootElement
            : null;

        string Cfg(string key, string fallback) =>
            settings?.TryGetProperty(key, out var v) == true ? v.GetString()! : fallback;

        var dbPath    = Environment.GetEnvironmentVariable("KNOWLEDGE_DB")
                     ?? Cfg("KnowledgeDb",  Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "knowledge.db")));
        var docsDir   = Environment.GetEnvironmentVariable("KNOWLEDGE_DIR")
                     ?? Cfg("KnowledgeDir", Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "knowledge")));
        var skillsDir = Environment.GetEnvironmentVariable("SKILLS_DIR")
                     ?? Cfg("SkillsDir",    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills"));

        var indexHtmlPath = Path.Combine(AppContext.BaseDirectory, "index.html");
        var staticDir     = Path.GetDirectoryName(indexHtmlPath)!;

        return (dbPath, docsDir, skillsDir, indexHtmlPath, staticDir);
    }
}
```
> Nota: `ResolveIndexHtml` se eliminó — queda inline en `Load()` como una sola línea.

- [ ] Reemplazar `src/Api/Models/Models.cs`:
```csharp
using System.Text.Json.Serialization;

namespace KnowledgeSearch;

internal record SearchResult(string Title, string Section, string Path, int Line, string Content);
internal record IndexResult(int Added, int Updated, int Deleted);
internal record HealthResult(string Status);
internal record SkillSummary(string Name, string Description, string DirName);

public record LogEvent(
    [property: JsonPropertyName("ts")]   string Ts,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("path")] string Path);

[JsonSerializable(typeof(List<SearchResult>))]
[JsonSerializable(typeof(List<SkillSummary>))]
[JsonSerializable(typeof(List<LogEvent>))]
[JsonSerializable(typeof(LogEvent))]
[JsonSerializable(typeof(IndexResult))]
[JsonSerializable(typeof(HealthResult))]
[JsonSerializable(typeof(string))]
public partial class AppJsonContext : JsonSerializerContext { }
```

- [ ] Reemplazar `src/Api/Services/ILogService.cs`:
```csharp
using System.Threading.Channels;

namespace KnowledgeSearch;

public interface ILogService
{
    void Append(LogEvent ev);
    List<LogEvent> ReadLast(int n);
    Channel<LogEvent> Subscribe();
    void Unsubscribe(ChannelWriter<LogEvent> writer);
}
```

- [ ] Reemplazar `src/Api/Services/LogService.cs`:
```csharp
using System.Text.Json;
using System.Threading.Channels;

namespace KnowledgeSearch;

internal sealed class LogService(string logPath) : ILogService
{
    private readonly object _lock = new();
    private readonly List<ChannelWriter<LogEvent>> _subs = [];

    // Case-insensitive options for reading old log entries that may have
    // been written with a different casing (e.g. PascalCase before the fix).
    private static readonly JsonSerializerOptions _readOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolverChain = { AppJsonContext.Default },
    };

    public void Append(LogEvent ev)
    {
        var line = JsonSerializer.Serialize(ev, AppJsonContext.Default.LogEvent);
        File.AppendAllText(logPath, line + "\n");
        lock (_lock)
        {
            foreach (var sub in _subs)
            {
                sub.TryWrite(ev);
            }
        }
    }

    public List<LogEvent> ReadLast(int n)
    {
        if (!File.Exists(logPath))
        {
            return [];
        }

        var lines = File.ReadAllLines(logPath);
        var result = new List<LogEvent>();
        foreach (var line in lines.TakeLast(n))
        {
            try
            {
                var ev = JsonSerializer.Deserialize<LogEvent>(line, _readOpts);
                if (ev is not null)
                {
                    result.Add(ev);
                }
            }
            catch (Exception)
            {
                // Best-effort: malformed log lines are skipped silently.
            }
        }
        return result;
    }

    public Channel<LogEvent> Subscribe()
    {
        var channel = Channel.CreateUnbounded<LogEvent>();
        lock (_lock)
        {
            _subs.Add(channel.Writer);
        }
        return channel;
    }

    public void Unsubscribe(ChannelWriter<LogEvent> writer)
    {
        lock (_lock)
        {
            _subs.Remove(writer);
        }
        writer.TryComplete();
    }
}
```

- [ ] Reemplazar `src/Api/Services/WatcherService.cs`:
```csharp
namespace KnowledgeSearch;

internal sealed class WatcherService(string docsDir, string dbPath, ILogService log) : BackgroundService
{
    private readonly Dictionary<string, Timer> _debounce = [];
    private readonly object _debounceLock = new();

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(docsDir))
        {
            return Task.CompletedTask;
        }

        var watcher = new FileSystemWatcher(docsDir)
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

        cancellationToken.Register(() => watcher.Dispose());
        return Task.Delay(Timeout.Infinite, cancellationToken);
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
            if (!File.Exists(path))
            {
                return;
            }

            using var connection = DbService.Open(dbPath);
            DbService.EnsureSchema(connection);

            // Mtime check is intentionally skipped here — the watcher already guarantees
            // a change occurred. Checking mtime would cause false negatives for atomic writes
            // (write-to-temp + rename) which preserve the original file's LastWriteTime.
            var modifiedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
            var stored     = DbService.QueryFirstLong(connection, "SELECT last_modified FROM docs_meta WHERE path=@path", path);

            if (stored is not null)
            {
                DbService.Execute(connection, "DELETE FROM docs WHERE path=@path", path);
            }

            DbService.IndexFile(connection, path, Path.GetFileNameWithoutExtension(path));
            DbService.Execute(connection,
                "INSERT INTO docs_meta(path,last_modified) VALUES(@path,@modifiedAt) ON CONFLICT(path) DO UPDATE SET last_modified=excluded.last_modified",
                path, modifiedAt);

            var relativePath = Path.GetRelativePath(docsDir, path).Replace('\\', '/');
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), eventType, relativePath));
        }
        catch (Exception)
        {
            // Best-effort: watcher operations must not crash the host.
        }
    }

    internal void ProcessDelete(string path)
    {
        try
        {
            using var connection = DbService.Open(dbPath);
            DbService.Execute(connection, "DELETE FROM docs      WHERE path=@path", path);
            DbService.Execute(connection, "DELETE FROM docs_meta WHERE path=@path", path);

            var relativePath = Path.GetRelativePath(docsDir, path).Replace('\\', '/');
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "deleted", relativePath));
        }
        catch (Exception)
        {
            // Best-effort: watcher operations must not crash the host.
        }
    }
}
```

- [ ] Agregar `internal` a los endpoint classes en `src/Api/Endpoints/SearchEndpoints.cs`:
```csharp
// Cambiar la primera línea de la clase de:
static class SearchEndpoints
// a:
internal static class SearchEndpoints
```

- [ ] Lo mismo para `SkillsEndpoints.cs`, `StaticEndpoints.cs`, `EventsEndpoints.cs`:
```csharp
internal static class SkillsEndpoints  // SkillsEndpoints.cs
internal static class StaticEndpoints  // StaticEndpoints.cs
internal static class EventsEndpoints  // EventsEndpoints.cs
```

- [ ] Verificar que compila:
```bash
dotnet build D:/knowledge-search/src/Api/Api.csproj
```
Expected: `Build succeeded.`

- [ ] Commit:
```bash
git -C "D:/knowledge-search" add -A
git -C "D:/knowledge-search" commit -m "refactor: add access modifiers and simplify AppConfig"
```

---

## Task 6: Renombrar helpers de `DbService`

**Files:**
- Modify: `src/Api/Persistence/DbService.cs`
- Modify: `src/Api/Endpoints/SearchEndpoints.cs`  ← usa `ExecP`, `ExecP2`, `QueryLong`

- [ ] Reemplazar `src/Api/Persistence/DbService.cs` completo:
```csharp
using Microsoft.Data.Sqlite;

namespace KnowledgeSearch;

internal static class DbService
{
    private const int SchemaVersion = 3;

    public static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        Execute(connection, "PRAGMA journal_mode=WAL");
        Execute(connection, "PRAGMA cache_size=-32000");
        Execute(connection, "PRAGMA synchronous=NORMAL");
        return connection;
    }

    public static void EnsureSchema(SqliteConnection connection)
    {
        Execute(connection, "CREATE TABLE IF NOT EXISTS schema_info(version INTEGER NOT NULL)");
        long? version = QuerySchemaVersion(connection);

        if (version == SchemaVersion)
        {
            return;
        }

        Execute(connection, "DROP TABLE IF EXISTS docs");
        Execute(connection, "DROP TABLE IF EXISTS docs_meta");
        Execute(connection, """
            CREATE VIRTUAL TABLE docs USING fts5(
                title, section, content,
                path UNINDEXED, line UNINDEXED,
                tokenize='trigram')
            """);
        Execute(connection, "CREATE TABLE docs_meta(path TEXT PRIMARY KEY, last_modified INTEGER NOT NULL)");

        if (version is null)
        {
            Execute(connection, $"INSERT INTO schema_info(version) VALUES({SchemaVersion})");
        }
        else
        {
            Execute(connection, $"UPDATE schema_info SET version={SchemaVersion}");
        }
    }

    // ── Indexing ──────────────────────────────────────────────────────────

    public static void IndexFile(SqliteConnection connection, string path, string title)
    {
        var lines   = File.ReadAllLines(path);
        var buffer  = new List<string>();
        int start   = 1;
        string section = title;

        void Flush()
        {
            if (buffer.Count == 0)
            {
                return;
            }

            if (buffer.All(l => string.IsNullOrWhiteSpace(l) || l.TrimStart().StartsWith('#')))
            {
                return;
            }

            using var cmd = new SqliteCommand(
                "INSERT INTO docs(title,section,content,path,line) VALUES(@title,@section,@content,@path,@line)", connection);
            cmd.Parameters.AddWithValue("@title",   title);
            cmd.Parameters.AddWithValue("@section", section);
            cmd.Parameters.AddWithValue("@content", string.Join("\n", buffer).Trim());
            cmd.Parameters.AddWithValue("@path",    path);
            cmd.Parameters.AddWithValue("@line",    start);
            cmd.ExecuteNonQuery();
            buffer.Clear();
        }

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('#'))
            {
                Flush();
                start   = i + 1;
                section = lines[i].TrimStart('#').Trim();
            }

            buffer.Add(lines[i]);
        }

        Flush();
    }

    public static string BuildFtsQuery(string query)
        => query.Trim().Contains(' ')
            ? string.Join(" OR ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => "\"" + w.Replace("\"", "\"\"") + "\""))
            : "\"" + query.Replace("\"", "\"\"") + "\"";

    // ── Helpers ────────────────────────────────────────────────────────────

    public static void Execute(SqliteConnection connection, string sql)
    {
        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    public static void Execute(SqliteConnection connection, string sql, string path)
    {
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@path", path);
        command.ExecuteNonQuery();
    }

    public static void Execute(SqliteConnection connection, string sql, string path, long modifiedAt)
    {
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@path",       path);
        command.Parameters.AddWithValue("@modifiedAt", modifiedAt);
        command.ExecuteNonQuery();
    }

    public static long? QueryFirstLong(SqliteConnection connection, string sql, string path)
    {
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@path", path);
        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : Convert.ToInt64(result);
    }

    private static long? QuerySchemaVersion(SqliteConnection connection)
    {
        using var command = new SqliteCommand("SELECT version FROM schema_info LIMIT 1", connection);
        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : Convert.ToInt64(result);
    }
}
```

- [ ] Actualizar `src/Api/Endpoints/SearchEndpoints.cs` para usar los nuevos nombres. Reemplazar el archivo completo:
```csharp
using Microsoft.Data.Sqlite;

namespace KnowledgeSearch;

internal static class SearchEndpoints
{
    public static void MapSearchRoutes(this WebApplication app, string dbPath, string docsDir)
    {
        app.MapGet("/search", (string q, int limit = 5) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest("Falta parámetro q");
            }

            using var connection = DbService.Open(dbPath);

            var results = new List<SearchResult>();
            using var cmd = new SqliteCommand(
                "SELECT title,section,path,line,content FROM docs WHERE docs MATCH @query ORDER BY bm25(docs,10,5,1) LIMIT @limit",
                connection);
            cmd.Parameters.AddWithValue("@query", DbService.BuildFtsQuery(q));
            cmd.Parameters.AddWithValue("@limit", limit);

            try
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new SearchResult(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt32(3),
                        reader.GetString(4)));
                }
            }
            catch (SqliteException ex)
            {
                return Results.BadRequest($"Query inválida: {ex.Message}");
            }

            return Results.Ok(results);
        });

        app.MapPost("/index", () =>
        {
            if (!Directory.Exists(docsDir))
            {
                return Results.BadRequest(new IndexResult(0, 0, 0));
            }

            using var connection = DbService.Open(dbPath);
            DbService.EnsureSchema(connection);

            var separator = Path.DirectorySeparatorChar;
            var files = Directory.EnumerateFiles(docsDir, "*.md",  SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(docsDir, "*.mkd", SearchOption.AllDirectories))
                .Select(Path.GetFullPath)
                .Where(f => !f.Contains($"{separator}node_modules{separator}") && !f.Contains($"{separator}.git{separator}"))
                .ToHashSet();

            var indexed = new HashSet<string>();
            using (var cmd = new SqliteCommand("SELECT path FROM docs_meta", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    indexed.Add(reader.GetString(0));
                }
            }

            int deleted = 0, added = 0, updated = 0;

            foreach (var removed in indexed.Except(files))
            {
                DbService.Execute(connection, "DELETE FROM docs      WHERE path=@path", removed);
                DbService.Execute(connection, "DELETE FROM docs_meta WHERE path=@path", removed);
                deleted++;
            }

            foreach (var file in files)
            {
                var modifiedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds();
                var stored     = DbService.QueryFirstLong(connection, "SELECT last_modified FROM docs_meta WHERE path=@path", file);

                if (stored == modifiedAt)
                {
                    continue;
                }

                var isNew = stored is null;
                if (!isNew)
                {
                    DbService.Execute(connection, "DELETE FROM docs WHERE path=@path", file);
                }

                DbService.IndexFile(connection, file, Path.GetFileNameWithoutExtension(file));
                DbService.Execute(connection,
                    "INSERT INTO docs_meta(path,last_modified) VALUES(@path,@modifiedAt) ON CONFLICT(path) DO UPDATE SET last_modified=excluded.last_modified",
                    file, modifiedAt);

                if (isNew)
                {
                    added++;
                }
                else
                {
                    updated++;
                }
            }

            return Results.Ok(new IndexResult(added, updated, deleted));
        });

        app.MapGet("/file", (string path) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.BadRequest("Falta parámetro path");
            }

            var fullPath = Path.GetFullPath(path);
            var docsRoot = Path.GetFullPath(docsDir);

            if (!fullPath.StartsWith(docsRoot, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest("Ruta fuera del knowledge dir");
            }

            if (!File.Exists(fullPath))
            {
                return Results.NotFound();
            }

            return Results.Text(File.ReadAllText(fullPath), "text/plain; charset=utf-8");
        });

        app.MapGet("/health", () => Results.Ok(new HealthResult("ok")));
    }
}
```

- [ ] Verificar que compila:
```bash
dotnet build D:/knowledge-search/src/Api/Api.csproj
```
Expected: `Build succeeded.`

- [ ] Commit:
```bash
git -C "D:/knowledge-search" add -A
git -C "D:/knowledge-search" commit -m "refactor: rename DbService helpers and SQL parameters"
```

---

## Task 7: Agregar `Solution Items` al `.slnx`

**Files:**
- Modify: `knowledge-search.slnx`

- [ ] Reemplazar `knowledge-search.slnx`:
```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/Api/Api.csproj" />
  </Folder>
  <Folder Name="/test/">
    <Project Path="test/Api.Tests/Api.Tests.csproj" />
  </Folder>
  <Folder Name="/Solution Items/">
    <File Path=".editorconfig" />
    <File Path="Directory.Build.props" />
    <File Path="NuGet.config" />
    <File Path=".gitignore" />
  </Folder>
</Solution>
```

- [ ] Verificar que el build sigue funcionando:
```bash
dotnet build D:/knowledge-search/src/Api/Api.csproj
```
Expected: `Build succeeded.`

- [ ] Commit y push:
```bash
git -C "D:/knowledge-search" add knowledge-search.slnx
git -C "D:/knowledge-search" commit -m "chore: add Solution Items folder to slnx"
git -C "D:/knowledge-search" push
```

---

## Task 8: Smoke test final

- [ ] Matar proceso API existente si hay uno corriendo:
```bash
Get-Process -Name "Api" -ErrorAction SilentlyContinue | Stop-Process -Force
```

- [ ] Levantar la API y verificar que responde:
```bash
Start-Process dotnet -ArgumentList "run --project D:/knowledge-search/src/Api" -NoNewWindow
Start-Sleep -Seconds 7
Invoke-RestMethod "http://localhost:5111/health"
```
Expected: `{ status = ok }`

- [ ] Verificar búsqueda:
```bash
Invoke-RestMethod "http://localhost:5111/search?q=mediator&limit=3"
```
Expected: array de resultados con `title`, `section`, `path`, `line`, `content`.

- [ ] Verificar log y SSE:
```bash
Invoke-RestMethod "http://localhost:5111/log"
```
Expected: array JSON (puede estar vacío si no hubo cambios recientes).

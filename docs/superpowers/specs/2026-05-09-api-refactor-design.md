# API Refactor — Design Spec

**Date:** 2026-05-09  
**Scope:** `D:/knowledge-search` — backend .NET API only  
**Approach:** All-in-one (single PR, atomic commits per concern)

---

## Goal

Apply the same conventions used in DevHub (`.editorconfig`, `Directory.Build.props`) to knowledge-search as a standalone repo. Improve folder structure, naming clarity, and code quality. Clean up dead code and runtime artifacts.

---

## 1. Folder & Solution Structure

### `src/Api/` — new layout

```
src/Api/
├── Program.cs
├── AppConfig.cs               ← stays at root (startup-coupled)
├── Api.csproj
├── appsettings.example.json
├── index.html
├── assets/
├── Endpoints/                 ← unchanged
│   ├── SearchEndpoints.cs
│   ├── SkillsEndpoints.cs
│   ├── StaticEndpoints.cs
│   └── EventsEndpoints.cs
├── Models/                    ← new folder (moved from root)
│   └── Models.cs
├── Persistence/               ← renamed from Db/
│   └── DbService.cs
└── Services/                  ← new folder
    ├── ILogService.cs
    ├── LogService.cs
    └── WatcherService.cs
```

### `knowledge-search.slnx` — add Solution Items virtual folder

```
/src/             → Api.csproj
/test/            → Api.Tests.csproj
/Solution Items/  ← new virtual folder
    .editorconfig
    Directory.Build.props
    NuGet.config
    .gitignore
```

The `Solution Items` folder is virtual (IDE display only). The files live at the repo root.

### Files to delete

| File | Reason |
|------|--------|
| `src/Api/marked.min.js` | Dead code — not referenced in `index.html` or any source |
| `.api.log` / `.api.err` | Temporary runtime files created during debugging |
| `knowledge.log` | Runtime file — already pattern in `.gitignore` |

### `.gitignore` additions

```
# Runtime logs
.api.log
.api.err
knowledge.log
```

---

## 2. New Config Files (repo root)

### `Directory.Build.props`

Inherited automatically by all `.csproj` files in the repo. Allows `Api.csproj` to remove duplicated properties.

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

### `.editorconfig`

Same conventions as DevHub, applied at repo root:

- `indent_style = space`, `indent_size = 4`, `end_of_line = crlf`
- C#: `csharp_prefer_braces = true:error`
- C#: `var` when type is apparent
- C#: file-scoped namespaces
- C#: access modifiers always required
- C#: private fields with `_` prefix, camelCase
- C#: readonly fields preferred
- C#: no multiple consecutive blank lines
- JSON: `indent_size = 2`
- Markdown: `trim_trailing_whitespace = false`

### `Api.csproj` — slim down

Properties now inherited from `Directory.Build.props` are removed. Only project-specific config remains:

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

---

## 3. Code Changes

### 3.1 Access modifiers

All classes get explicit access modifiers. The `.editorconfig` will enforce this going forward.

| Class | Before | After |
|-------|--------|-------|
| `AppConfig` | `static class` | `internal static class` |
| `DbService` | `static class` | `internal static class` |
| `LogService` | `class` | `internal sealed class` |
| `WatcherService` | `class` | `internal sealed class` |
| Records in `Models.cs` | `record` | `internal record` (except `LogEvent` and `AppJsonContext` which stay `public`) |

### 3.2 `AppConfig.ResolveIndexHtml` — simplify

Remove the 4-candidate fallback chain. `index.html` is always in `AppContext.BaseDirectory` at runtime (both `dotnet run` and published).

```csharp
// Before: FirstOrDefault over 4 candidate paths
// After:
static string ResolveIndexHtml() =>
    Path.Combine(AppContext.BaseDirectory, "index.html");
```

### 3.3 `DbService` — rename helpers and SQL parameters

Method renames:

| Before | After |
|--------|-------|
| `ExecP(con, sql, path)` | `Execute(con, sql, path)` |
| `ExecP2(con, sql, path, mtime)` | `Execute(con, sql, path, mtime)` (overload) |
| `QueryLong(con, sql, path)` | `QueryFirstLong(con, sql, path)` |
| `QueryScalar(con, sql, q, limit)` | `Search(con, query, limit)` |

SQL parameter names (inside each method body):

| Before | After |
|--------|-------|
| `@p` | `@path` |
| `@m` | `@modifiedAt` |
| `@q` | `@query` |
| `@lim` | `@limit` |

### 3.4 Empty catch blocks

`WatcherService.ProcessChange`, `WatcherService.ProcessDelete`, and `LogService.ReadLast` have bare `catch {}` blocks. These operations are intentionally best-effort (watcher and log reads should never crash the app). Replace with explicit comment:

```csharp
catch (Exception)
{
    // Best-effort: watcher/log operations must not crash the host.
}
```

---

## 4. Commit Order

Each commit must compile and pass before the next:

1. `chore: delete dead code and add runtime files to .gitignore`
2. `chore: add .editorconfig and Directory.Build.props`
3. `refactor: slim down Api.csproj (properties moved to Directory.Build.props)`
4. `refactor: move files to Services/, Models/, Persistence/ folders`
5. `refactor: add access modifiers to all classes`
6. `refactor: simplify AppConfig.ResolveIndexHtml`
7. `refactor: rename DbService helpers and SQL parameters`
8. `refactor: document best-effort catch blocks`
9. `chore: add Solution Items folder to knowledge-search.slnx`

---

## Out of Scope

- Frontend React code (separate concern)
- Test project changes
- Adding new features
- Changing the SQLite schema or search behavior

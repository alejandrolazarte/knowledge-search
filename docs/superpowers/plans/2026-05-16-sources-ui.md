# Sources UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first functional Sources screen: read, edit, import/export, and save `data/sources.json` from the local UI.

**Architecture:** Add a small backend service around source configuration persistence, expose focused Minimal API endpoints, then build a dense React `SourcesView` using the approved list-plus-detail layout. `DbService` roots stay startup-bound for now; `/sources` and `/roots` should reflect saved JSON through the new source configuration service.

**Tech Stack:** ASP.NET Core Minimal API (.NET 10), System.Text.Json source generation, xUnit/Shouldly, React 19, TypeScript, Tailwind, Vite. Backend tests run on host; frontend build runs through the existing Podman script.

---

## File Structure

- Create `src/Api/Configuration/ISourceConfigurationService.cs`: interface for loading, saving, exporting, and deriving roots.
- Create `src/Api/Configuration/SourceConfigurationService.cs`: persistence and validation for `data/sources.json`.
- Modify `src/Api/Configuration/SourceConfiguration.cs`: add API response/request records if needed and conversion helpers.
- Create `src/Api/Endpoints/SourcesEndpoints.cs`: `GET /sources`, `PUT /sources`, `GET /sources/export`.
- Modify `src/Api/Endpoints/SearchEndpoints.cs`: make `/roots` use `ISourceConfigurationService` instead of `IDbService`.
- Modify `src/Api/Program.cs`: register `ISourceConfigurationService`; keep `DbService` initialized from startup roots.
- Modify `src/Api/Models/Models.cs`: add JSON source generation metadata for new request/response types.
- Create `test/Api.Tests/When_SourceEndpointsAreRequested.cs`: endpoint tests for fallback, save, validation, export, and roots refresh.
- Modify `app/src/types.ts`: add source configuration TypeScript types.
- Create `app/src/components/SourcesView.tsx`: approved layout B implementation.
- Modify `app/src/App.tsx`: add `sources` view and header label.
- Modify `app/src/components/Sidebar.tsx`: add Sources nav item.

---

### Task 1: Backend Persistence Service

**Files:**
- Create: `src/Api/Configuration/ISourceConfigurationService.cs`
- Create: `src/Api/Configuration/SourceConfigurationService.cs`
- Modify: `src/Api/Configuration/SourceConfiguration.cs`
- Test: `test/Api.Tests/When_SourceEndpointsAreRequested.cs`

- [ ] **Step 1: Write failing service tests through endpoints**

Create `test/Api.Tests/When_SourceEndpointsAreRequested.cs` with tests that drive the service through HTTP.

```csharp
using System.Net;
using System.Net.Http.Json;
using KnowledgeSearch;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_SourceEndpointsAreRequested : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public When_SourceEndpointsAreRequested()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task Then_GetSourcesReturnsFallbackConfiguration()
    {
        var docsDir = Directory.CreateDirectory(Path.Combine(_tempDir, "docs")).FullName;
        using var app = CreateApp(new Dictionary<string, string?>
        {
            ["SourcesConfig"] = Path.Combine(_tempDir, "data", "sources.json"),
            ["KnowledgeDirs"] = docsDir,
        });
        var client = app.CreateClient();

        var response = await client.GetFromJsonAsync<SourceConfigurationFile>("/sources");

        response.ShouldNotBeNull();
        response.Sources.Count.ShouldBe(1);
        response.Sources[0].Kind.ShouldBe(SourceKind.Knowledge);
        response.Sources[0].HostPath.ShouldBe(Path.GetFullPath(docsDir));
        response.Sources[0].IndexDocs.ShouldBeTrue();
        response.Sources[0].IndexCode.ShouldBeFalse();
    }

    [Fact]
    public async Task Then_PutSourcesSavesIndentedJson()
    {
        var configPath = Path.Combine(_tempDir, "data", "sources.json");
        using var app = CreateApp(new Dictionary<string, string?>
        {
            ["SourcesConfig"] = configPath,
            ["KnowledgeDirs"] = Path.Combine(_tempDir, "fallback"),
        });
        var client = app.CreateClient();
        var source = SourceDefinition.FromConfiguredSource(ConfiguredSource.Create(
            "orders-ms",
            "orders-ms",
            SourceKind.Repository,
            Path.Combine(_tempDir, "orders-ms")));
        var payload = new SourceConfigurationFile(1, [source]);

        var saveResponse = await client.PutAsJsonAsync("/sources", payload);

        saveResponse.EnsureSuccessStatusCode();
        File.Exists(configPath).ShouldBeTrue();
        var saved = await File.ReadAllTextAsync(configPath);
        saved.ShouldContain(Environment.NewLine);
        saved.ShouldContain("\"id\": \"orders-ms\"");
        var loaded = await client.GetFromJsonAsync<SourceConfigurationFile>("/sources");
        loaded!.Sources.Single().Id.ShouldBe("orders-ms");
    }

    [Fact]
    public async Task Then_PutSourcesRejectsDuplicateIds()
    {
        using var app = CreateApp(new Dictionary<string, string?>
        {
            ["SourcesConfig"] = Path.Combine(_tempDir, "sources.json"),
        });
        var client = app.CreateClient();
        var path = Path.Combine(_tempDir, "repo");
        var payload = new SourceConfigurationFile(1,
        [
            SourceDefinition.FromConfiguredSource(ConfiguredSource.Create("repo", "Repo A", SourceKind.Repository, path)),
            SourceDefinition.FromConfiguredSource(ConfiguredSource.Create("repo", "Repo B", SourceKind.Repository, path)),
        ]);

        var response = await client.PutAsJsonAsync("/sources", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResult>();
        error!.Error.ShouldContain("duplicado");
    }

    [Fact]
    public async Task Then_ExportReturnsGeneratedFallbackWhenFileDoesNotExist()
    {
        var docsDir = Directory.CreateDirectory(Path.Combine(_tempDir, "docs")).FullName;
        using var app = CreateApp(new Dictionary<string, string?>
        {
            ["SourcesConfig"] = Path.Combine(_tempDir, "missing", "sources.json"),
            ["KnowledgeDirs"] = docsDir,
        });
        var client = app.CreateClient();

        var exported = await client.GetStringAsync("/sources/export");

        exported.ShouldContain("\"version\"");
        exported.ShouldContain("docs");
    }

    [Fact]
    public async Task Then_RootsReflectSavedSourcesWithIndexDocs()
    {
        var docsPath = Path.Combine(_tempDir, "docs");
        var codeOnlyPath = Path.Combine(_tempDir, "code-only");
        using var app = CreateApp(new Dictionary<string, string?>
        {
            ["SourcesConfig"] = Path.Combine(_tempDir, "data", "sources.json"),
        });
        var client = app.CreateClient();
        var payload = new SourceConfigurationFile(1,
        [
            SourceDefinition.FromConfiguredSource(ConfiguredSource.Create("docs", "docs", SourceKind.Knowledge, docsPath)),
            SourceDefinition.FromConfiguredSource(ConfiguredSource.Create("code", "code", SourceKind.Repository, codeOnlyPath, indexDocs: false)),
        ]);
        (await client.PutAsJsonAsync("/sources", payload)).EnsureSuccessStatusCode();

        var roots = await client.GetFromJsonAsync<List<string>>("/roots");

        roots.ShouldBe(["docs"]);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private WebApplicationFactory<Program> CreateApp(Dictionary<string, string?> settings)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(settings);
                });
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ILogService>(_ => new LogService(Path.Combine(_tempDir, "test.log")));
                });
            });
    }
}
```

- [ ] **Step 2: Run endpoint tests and verify RED**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter SourceEndpoints
```

Expected: build fails because `/sources` endpoints and `SourceDefinition.FromConfiguredSource` do not exist.

- [ ] **Step 3: Add conversion helper**

Modify `src/Api/Configuration/SourceConfiguration.cs` to add:

```csharp
public static SourceDefinition FromConfiguredSource(ConfiguredSource source)
{
    return new SourceDefinition(
        source.Id,
        source.Name,
        source.Kind,
        source.HostPath,
        source.IndexCode,
        source.IndexDocs,
        source.DocIncludes,
        source.CodeIncludes,
        source.Excludes);
}
```

Place the method inside `SourceDefinition`.

- [ ] **Step 4: Create service interface**

Create `src/Api/Configuration/ISourceConfigurationService.cs`:

```csharp
namespace KnowledgeSearch;

internal interface ISourceConfigurationService
{
    SourceConfigurationFile GetConfiguration();
    IReadOnlyList<string> GetKnowledgeRootNames();
    string ExportJson();
    SaveSourcesResult Save(SourceConfigurationFile configuration);
}

internal sealed record SaveSourcesResult(bool Success, string? Error);
```

- [ ] **Step 5: Implement service**

Create `src/Api/Configuration/SourceConfigurationService.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace KnowledgeSearch;

internal sealed class SourceConfigurationService(
    IConfiguration configuration,
    Func<string, string?> getEnvironmentVariable) : ISourceConfigurationService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    static SourceConfigurationService()
    {
        _jsonOptions.Converters.Add(new JsonStringEnumConverter<SourceKind>());
    }

    private readonly object _lock = new();

    public SourceConfigurationFile GetConfiguration()
    {
        lock (_lock)
        {
            return ReadConfiguration();
        }
    }

    public IReadOnlyList<string> GetKnowledgeRootNames()
    {
        return GetConfiguration()
            .Sources
            .Select(source => source.ToConfiguredSource())
            .Where(source => source.IndexDocs)
            .Select(source => Path.GetFileName(Path.TrimEndingDirectorySeparator(source.HostPath)))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
    }

    public string ExportJson()
    {
        return JsonSerializer.Serialize(GetConfiguration(), _jsonOptions);
    }

    public SaveSourcesResult Save(SourceConfigurationFile configuration)
    {
        var normalized = Normalize(configuration);
        var validationError = Validate(normalized);
        if (validationError is not null)
        {
            return new SaveSourcesResult(false, validationError);
        }

        lock (_lock)
        {
            var path = GetSourcesConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(normalized, _jsonOptions));
        }

        return new SaveSourcesResult(true, null);
    }

    private SourceConfigurationFile ReadConfiguration()
    {
        var path = GetSourcesConfigPath();
        if (File.Exists(path))
        {
            var fromFile = JsonSerializer.Deserialize<SourceConfigurationFile>(
                File.ReadAllText(path),
                _jsonOptions);
            return Normalize(fromFile ?? new SourceConfigurationFile(1, []));
        }

        return Normalize(new SourceConfigurationFile(
            1,
            AppConfiguration.ResolveSources(configuration, getEnvironmentVariable)
                .Sources
                .Select(SourceDefinition.FromConfiguredSource)
                .ToArray()));
    }

    private SourceConfigurationFile Normalize(SourceConfigurationFile configuration)
    {
        var sources = configuration.Sources
            .Select(source => SourceDefinition.FromConfiguredSource(source.ToConfiguredSource()))
            .ToArray();
        return new SourceConfigurationFile(configuration.Version <= 0 ? 1 : configuration.Version, sources);
    }

    private static string? Validate(SourceConfigurationFile configuration)
    {
        if (configuration.Sources is null)
        {
            return "sources no puede ser null.";
        }

        foreach (var source in configuration.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.Id)
                || string.IsNullOrWhiteSpace(source.Name)
                || string.IsNullOrWhiteSpace(source.HostPath))
            {
                return "Cada source requiere id, name y hostPath.";
            }
        }

        var duplicateId = configuration.Sources
            .GroupBy(source => source.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        return duplicateId is null ? null : $"id duplicado: {duplicateId}";
    }

    private string GetSourcesConfigPath()
    {
        return getEnvironmentVariable("SOURCES_CONFIG")
            ?? configuration["SourcesConfig"]
            ?? Path.Combine(AppContext.BaseDirectory, "data", "sources.json");
    }
}
```

- [ ] **Step 6: Run endpoint tests and observe remaining failures**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter SourceEndpoints
```

Expected: build succeeds but tests fail with 404 for `/sources` endpoints.

---

### Task 2: Backend Sources Endpoints

**Files:**
- Create: `src/Api/Endpoints/SourcesEndpoints.cs`
- Modify: `src/Api/Endpoints/SearchEndpoints.cs`
- Modify: `src/Api/Program.cs`
- Modify: `src/Api/Models/Models.cs`
- Test: `test/Api.Tests/When_SourceEndpointsAreRequested.cs`

- [ ] **Step 1: Add endpoint mapper**

Create `src/Api/Endpoints/SourcesEndpoints.cs`:

```csharp
namespace KnowledgeSearch;

internal static class SourcesEndpoints
{
    public static void MapSourcesRoutes(this WebApplication app)
    {
        app.MapGet("/sources", (ISourceConfigurationService sources) =>
            Results.Ok(sources.GetConfiguration()));

        app.MapPut("/sources", (SourceConfigurationFile request, ISourceConfigurationService sources) =>
        {
            var result = sources.Save(request);
            return result.Success
                ? Results.Ok(sources.GetConfiguration())
                : Results.BadRequest(new ErrorResult(result.Error ?? "Configuración inválida."));
        });

        app.MapGet("/sources/export", (ISourceConfigurationService sources) =>
            Results.Text(sources.ExportJson(), "application/json; charset=utf-8"));
    }
}
```

- [ ] **Step 2: Register service and routes**

Modify `src/Api/Program.cs`:

```csharp
builder.Services.AddSingleton<ISourceConfigurationService>(_ =>
    new SourceConfigurationService(builder.Configuration, Environment.GetEnvironmentVariable));
```

Add this near other singleton registrations.

Then map routes before search routes:

```csharp
app.MapSourcesRoutes();
app.MapSearchRoutes();
```

- [ ] **Step 3: Update `/roots` to reflect saved JSON**

Modify the `/roots` endpoint in `src/Api/Endpoints/SearchEndpoints.cs`:

```csharp
app.MapGet("/roots", (ISourceConfigurationService sources) =>
    Results.Ok(sources.GetKnowledgeRootNames()));
```

This intentionally decouples `/roots` from the startup-bound `DbService`.

- [ ] **Step 4: Add JSON metadata**

Modify `src/Api/Models/Models.cs` to include:

```csharp
[JsonSerializable(typeof(SourceConfigurationFile))]
[JsonSerializable(typeof(SourceDefinition))]
[JsonSerializable(typeof(List<SourceDefinition>))]
[JsonSerializable(typeof(SaveSourcesResult))]
```

Keep existing source metadata if already present.

- [ ] **Step 5: Run focused endpoint tests and verify GREEN**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter SourceEndpoints
```

Expected: all `When_SourceEndpointsAreRequested` tests pass.

- [ ] **Step 6: Commit backend endpoints**

Run:

```powershell
git add src/Api/Configuration src/Api/Endpoints/SourcesEndpoints.cs src/Api/Endpoints/SearchEndpoints.cs src/Api/Program.cs src/Api/Models/Models.cs test/Api.Tests/When_SourceEndpointsAreRequested.cs
git commit -m "feat: add sources endpoints"
```

---

### Task 3: Frontend Sources View Types and Navigation

**Files:**
- Modify: `app/src/types.ts`
- Modify: `app/src/App.tsx`
- Modify: `app/src/components/Sidebar.tsx`
- Create: `app/src/components/SourcesView.tsx`

- [ ] **Step 1: Add TypeScript types**

Modify `app/src/types.ts`:

```ts
export type SourceKind = 'Knowledge' | 'Repository'

export interface SourceDefinition {
  id:           string
  name:         string
  kind:         SourceKind
  hostPath:     string
  indexCode:    boolean
  indexDocs:    boolean
  docIncludes:  string[]
  codeIncludes: string[]
  excludes:     string[]
}

export interface SourceConfigurationFile {
  version: number
  sources: SourceDefinition[]
}
```

- [ ] **Step 2: Add initial SourcesView component**

Create `app/src/components/SourcesView.tsx` with a loading/error shell:

```tsx
import { useEffect, useState } from 'react'
import type { SourceConfigurationFile, SourceDefinition, SourceKind } from '../types'

const DEFAULT_EXCLUDES = ['**/.git/**', '**/node_modules/**', '**/bin/**', '**/obj/**', '**/dist/**', '**/build/**', '**/.next/**', '**/coverage/**']
const REPOSITORY_DOC_INCLUDES = ['README.md', 'docs/**/*.md', 'specs/**/*.md', 'adr/**/*.md']
const KNOWLEDGE_DOC_INCLUDES = ['**/*.md', '**/*.mdx']
const CODE_INCLUDES = ['**/*.cs', '**/*.ts', '**/*.tsx', '**/*.js', '**/*.jsx', '**/*.py']

function lines(value: string[]): string {
  return value.join('\n')
}

function splitLines(value: string): string[] {
  return value.split(/\r?\n/).map(line => line.trim()).filter(Boolean)
}

function createSource(kind: SourceKind, count: number): SourceDefinition {
  const id = `source-${count + 1}`
  return {
    id,
    name: id,
    kind,
    hostPath: '',
    indexCode: kind === 'Repository',
    indexDocs: true,
    docIncludes: kind === 'Repository' ? REPOSITORY_DOC_INCLUDES : KNOWLEDGE_DOC_INCLUDES,
    codeIncludes: CODE_INCLUDES,
    excludes: DEFAULT_EXCLUDES,
  }
}

export function SourcesView() {
  const [config, setConfig] = useState<SourceConfigurationFile | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [status, setStatus] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    fetch('/sources')
      .then(async response => {
        if (!response.ok) throw new Error(await response.text())
        return response.json() as Promise<SourceConfigurationFile>
      })
      .then(next => {
        setConfig(next)
        setSelectedId(next.sources[0]?.id ?? null)
      })
      .catch(() => setError('No se pudo cargar sources.json'))
  }, [])

  if (error) {
    return <div className="flex-1 p-4 text-sm text-red-400">{error}</div>
  }

  if (!config) {
    return <div className="flex-1 p-4 text-sm text-gh-muted">Cargando sources…</div>
  }

  const selected = config.sources.find(source => source.id === selectedId) ?? config.sources[0] ?? null

  return (
    <div className="flex-1 overflow-hidden p-4">
      <div className="text-sm text-gh-muted">Sources UI shell loaded: {config.sources.length}</div>
      {selected && <div className="mt-2 text-sm text-gh-text">{selected.name}</div>}
      {status && <div className="mt-2 text-xs text-gh-muted">{status}</div>}
    </div>
  )
}
```

This shell proves API loading and navigation before building the full editor.

- [ ] **Step 3: Wire App view**

Modify `app/src/App.tsx`:

```tsx
import { SourcesView } from './components/SourcesView'
```

Change the view type:

```ts
type View = 'search' | 'repo-search' | 'skills' | 'graph' | 'sources'
```

Update header label:

```tsx
{view === 'search' ? 'Knowledge Search'
  : view === 'repo-search' ? 'Repo Search'
  : view === 'skills' ? 'Skills'
  : view === 'graph' ? 'Code Graph'
  : 'Sources'}
```

Render the new view:

```tsx
{view === 'sources' && (
  <SourcesView />
)}
```

- [ ] **Step 4: Wire Sidebar navigation**

Modify `app/src/components/Sidebar.tsx`:

```ts
type View = 'search' | 'repo-search' | 'skills' | 'graph' | 'sources'
```

Add a nav item after `Code Graph`:

```tsx
<NavItem icon={<SourcesIcon />} label="Sources"
  active={view === 'sources'} collapsed={collapsed} onClick={() => onView('sources')} />
```

Add icon:

```tsx
function SourcesIcon() {
  return <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
    <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 5.25h16.5M3.75 12h16.5M3.75 18.75h16.5M7.5 5.25v13.5"/>
  </svg>
}
```

- [ ] **Step 5: Build frontend through Podman**

Run:

```powershell
dotnet run scripts/podman/podman-dev.cs -- --build-frontend
```

Expected: frontend assets build successfully inside the container. If Podman is not running, start Podman Desktop and rerun.

---

### Task 4: Frontend Editing, Save, Import, Export

**Files:**
- Modify: `app/src/components/SourcesView.tsx`

- [ ] **Step 1: Replace shell with layout B**

Update `SourcesView` to render:

```tsx
return (
  <div className="flex-1 flex flex-col overflow-hidden p-4 gap-3">
    <div className="flex items-center gap-2 shrink-0">
      <button onClick={() => addSource('Repository')} className="bg-gh-accent hover:opacity-90 text-white text-sm px-3 py-1.5 rounded font-medium">
        Add folder
      </button>
      <button onClick={importJson} className="border border-gh-border bg-gh-surface text-gh-muted hover:text-gh-text text-sm px-3 py-1.5 rounded">
        Import JSON
      </button>
      <button onClick={exportJson} className="border border-gh-border bg-gh-surface text-gh-muted hover:text-gh-text text-sm px-3 py-1.5 rounded">
        Export JSON
      </button>
      <div className="flex-1" />
      {status && <span className="text-xs text-gh-muted">{status}</span>}
    </div>
    <div className="flex-1 min-h-0 grid grid-cols-[320px_minmax(0,1fr)] border border-gh-border rounded-lg overflow-hidden bg-gh-bg">
      <div className="border-r border-gh-border bg-gh-surface/40 overflow-y-auto">
        {config.sources.map(source => (
          <SourceListItem key={source.id} source={source} active={source.id === selectedId} onClick={() => setSelectedId(source.id)} />
        ))}
      </div>
      <div className="overflow-y-auto">
        {selected ? <SourceEditor source={selected} onChange={updateSelected} onDelete={deleteSelected} onSave={save} saving={saving} /> : <EmptyState />}
      </div>
    </div>
  </div>
)
```

Use helper components in the same file to keep the first version contained.

- [ ] **Step 2: Add editing helpers**

Inside `SourcesView.tsx`, add:

```tsx
const updateSelected = (patch: Partial<SourceDefinition>) => {
  if (!selected) return
  setConfig(prev => prev
    ? { ...prev, sources: prev.sources.map(source => source.id === selected.id ? { ...source, ...patch } : source) }
    : prev)
  setStatus('Unsaved changes')
}

const addSource = (kind: SourceKind) => {
  setConfig(prev => {
    const base = prev ?? { version: 1, sources: [] }
    const source = createSource(kind, base.sources.length)
    setSelectedId(source.id)
    setStatus('Unsaved changes')
    return { ...base, sources: [...base.sources, source] }
  })
}

const deleteSelected = () => {
  if (!selected) return
  setConfig(prev => {
    if (!prev) return prev
    const sources = prev.sources.filter(source => source.id !== selected.id)
    setSelectedId(sources[0]?.id ?? null)
    setStatus('Unsaved changes')
    return { ...prev, sources }
  })
}

const save = async () => {
  if (!config) return
  setSaving(true)
  setStatus('Saving…')
  try {
    const response = await fetch('/sources', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(config),
    })
    if (!response.ok) {
      const body = await response.json().catch(() => null) as { error?: string } | null
      throw new Error(body?.error ?? 'Error al guardar')
    }
    const saved = await response.json() as SourceConfigurationFile
    setConfig(saved)
    setStatus('Saved. Restart or reindex flow required for active index roots.')
  } catch (err) {
    setStatus(err instanceof Error ? err.message : 'Error al guardar')
  } finally {
    setSaving(false)
  }
}
```

- [ ] **Step 3: Add import/export helpers**

Use a hidden file input for import:

```tsx
const fileInputRef = useRef<HTMLInputElement>(null)

const importJson = () => fileInputRef.current?.click()

const onImportFile = async (event: React.ChangeEvent<HTMLInputElement>) => {
  const file = event.target.files?.[0]
  event.target.value = ''
  if (!file) return
  try {
    const parsed = JSON.parse(await file.text()) as SourceConfigurationFile
    if (!Array.isArray(parsed.sources)) throw new Error('sources debe ser un array')
    setConfig(parsed)
    setSelectedId(parsed.sources[0]?.id ?? null)
    setStatus('Imported JSON. Save changes to persist.')
  } catch {
    setStatus('JSON inválido')
  }
}

const exportJson = () => {
  if (!config) return
  const blob = new Blob([JSON.stringify(config, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = 'sources.json'
  link.click()
  URL.revokeObjectURL(url)
}
```

Render:

```tsx
<input ref={fileInputRef} type="file" accept="application/json,.json" className="hidden" onChange={onImportFile} />
```

- [ ] **Step 4: Add editor fields**

Implement fields using compact labels and existing Tailwind style:

```tsx
function SourceEditor({ source, onChange, onDelete, onSave, saving }: {
  source: SourceDefinition
  onChange: (patch: Partial<SourceDefinition>) => void
  onDelete: () => void
  onSave: () => void
  saving: boolean
}) {
  return (
    <div className="p-4 max-w-3xl">
      <div className="grid grid-cols-2 gap-3">
        <TextField label="Name" value={source.name} onChange={name => onChange({ name })} />
        <TextField label="ID" value={source.id} onChange={id => onChange({ id })} />
      </div>
      <TextField label="Host path" value={source.hostPath} onChange={hostPath => onChange({ hostPath })} />
      <div className="flex gap-2 my-3">
        <button onClick={() => onChange({ kind: 'Knowledge', indexCode: false, indexDocs: true, docIncludes: KNOWLEDGE_DOC_INCLUDES })}
          className={kindButton(source.kind === 'Knowledge')}>Knowledge</button>
        <button onClick={() => onChange({ kind: 'Repository', indexCode: true, indexDocs: true, docIncludes: REPOSITORY_DOC_INCLUDES })}
          className={kindButton(source.kind === 'Repository')}>Repository</button>
      </div>
      <div className="grid grid-cols-2 gap-3 my-3">
        <Toggle label="Index code" description="Repo Search + Code Graph" checked={source.indexCode} onChange={indexCode => onChange({ indexCode })} />
        <Toggle label="Index docs" description="Knowledge Search" checked={source.indexDocs} onChange={indexDocs => onChange({ indexDocs })} />
      </div>
      <RulesField label="Doc includes" value={source.docIncludes} onChange={docIncludes => onChange({ docIncludes })} />
      <RulesField label="Code includes" value={source.codeIncludes} onChange={codeIncludes => onChange({ codeIncludes })} />
      <RulesField label="Excludes" value={source.excludes} onChange={excludes => onChange({ excludes })} />
      <div className="flex gap-2 pt-2">
        <button onClick={onSave} disabled={saving} className="bg-gh-accent text-white text-sm px-3 py-1.5 rounded disabled:opacity-50">Save changes</button>
        <button onClick={onDelete} className="border border-gh-border text-red-400 hover:bg-red-500/10 text-sm px-3 py-1.5 rounded">Delete</button>
      </div>
    </div>
  )
}
```

Add these helpers in the same file:

```tsx
function TextField({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="block mb-3">
      <span className="block text-[10px] uppercase tracking-widest text-gh-muted mb-1">{label}</span>
      <input value={value} onChange={event => onChange(event.target.value)}
        className="w-full bg-gh-surface border border-gh-border rounded px-2 py-1.5 text-sm text-gh-text outline-none focus:border-gh-accent" />
    </label>
  )
}

function Toggle({ label, description, checked, onChange }: {
  label: string
  description: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <button onClick={() => onChange(!checked)}
      className={`text-left border rounded p-3 transition-colors ${checked ? 'border-gh-accent bg-gh-accent/10' : 'border-gh-border bg-gh-surface'}`}>
      <span className="block text-sm font-medium text-gh-text">{label}</span>
      <span className="block text-xs text-gh-muted mt-0.5">{description}</span>
    </button>
  )
}

function RulesField({ label, value, onChange }: {
  label: string
  value: string[]
  onChange: (value: string[]) => void
}) {
  return (
    <label className="block mb-3">
      <span className="block text-[10px] uppercase tracking-widest text-gh-muted mb-1">{label}</span>
      <textarea value={lines(value)} onChange={event => onChange(splitLines(event.target.value))}
        className="w-full min-h-24 bg-gh-surface border border-gh-border rounded px-2 py-1.5 font-mono text-xs text-gh-text outline-none focus:border-gh-accent" />
    </label>
  )
}

function SourceListItem({ source, active, onClick }: {
  source: SourceDefinition
  active: boolean
  onClick: () => void
}) {
  return (
    <button onClick={onClick}
      className={`block w-full text-left border-b border-gh-border p-3 transition-colors ${active ? 'bg-gh-card' : 'hover:bg-gh-surface'}`}>
      <div className="flex items-center gap-2">
        <span className="text-[10px] uppercase border border-gh-border rounded-full px-1.5 py-0.5 text-gh-muted">{source.kind}</span>
        <span className="text-sm font-medium text-gh-text truncate">{source.name}</span>
      </div>
      <div className="text-xs text-gh-muted truncate mt-1">{source.hostPath || 'No path set'}</div>
      <div className="flex gap-1 mt-2">
        <span className={`text-[10px] rounded-full px-1.5 py-0.5 border ${source.indexCode ? 'text-green-400 border-green-500/40' : 'text-gh-muted border-gh-border'}`}>code</span>
        <span className={`text-[10px] rounded-full px-1.5 py-0.5 border ${source.indexDocs ? 'text-green-400 border-green-500/40' : 'text-gh-muted border-gh-border'}`}>docs</span>
      </div>
    </button>
  )
}

function EmptyState() {
  return <div className="p-6 text-sm text-gh-muted">No source selected.</div>
}

function kindButton(active: boolean): string {
  return `flex-1 border rounded px-3 py-2 text-sm transition-colors ${active ? 'border-gh-accent bg-gh-accent/10 text-gh-text' : 'border-gh-border bg-gh-surface text-gh-muted hover:text-gh-text'}`
}
```

- [ ] **Step 5: Build frontend through Podman**

Run:

```powershell
dotnet run scripts/podman/podman-dev.cs -- --build-frontend
```

Expected: TypeScript and Vite build pass inside the container.

- [ ] **Step 6: Commit frontend view**

Run:

```powershell
git add app/src/types.ts app/src/App.tsx app/src/components/Sidebar.tsx app/src/components/SourcesView.tsx
git commit -m "feat: add sources ui"
```

---

### Task 5: Full Verification

**Files:**
- No new files.

- [ ] **Step 1: Run backend tests**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal
```

Expected: all tests pass.

- [ ] **Step 2: Build frontend through Podman**

Run:

```powershell
dotnet run scripts/podman/podman-dev.cs -- --build-frontend
```

Expected: frontend build passes and static assets update.

- [ ] **Step 3: Inspect diff**

Run:

```powershell
git status --short --branch
git diff --stat
```

Expected: only intended backend, frontend, tests, generated frontend assets, and this plan are changed.

- [ ] **Step 4: Commit plan if not already committed**

Run:

```powershell
git add docs/superpowers/plans/2026-05-16-sources-ui.md
git commit -m "docs: plan sources ui"
```

Skip this commit only if the plan was committed before implementation began.

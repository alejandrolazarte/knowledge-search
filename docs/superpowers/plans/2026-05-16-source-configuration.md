# Source Configuration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a first working source-configuration layer backed by `data/sources.json`, with repository and knowledge defaults, compatibility with `KNOWLEDGE_DIRS`, and backend wiring for documentation indexing roots.

**Architecture:** Introduce focused source configuration records and a loader in `AppConfiguration`. The loader prefers `data/sources.json`, falls back to `KNOWLEDGE_DIRS`/`KnowledgeDirs`, applies defaults by source kind, and exposes derived documentation roots to the existing `DbService` and `WatcherService`. This first implementation does not yet build the UI or separate indexer process; it creates the contract those features will consume.

**Tech Stack:** C#/.NET 10 Minimal API, `System.Text.Json` source generation, xUnit, Shouldly.

---

## File Structure

- Create `src/Api/Configuration/SourceConfiguration.cs`: source config records, defaults, path resolution helpers.
- Modify `src/Api/AppConfiguration.cs`: load `sources.json`, fall back to existing roots, derive knowledge roots.
- Modify `src/Api/Program.cs`: use the new configuration result.
- Modify `src/Api/Models/Models.cs`: register JSON metadata for source config API/serialization.
- Create `test/Api.Tests/When_SourceConfigurationResolves.cs`: tests for JSON loading, defaults, fallback, and repository docs roots.
- Keep `DbService` behavior unchanged except for receiving roots derived from sources.

---

### Task 1: Add Source Configuration Loading

**Files:**
- Create: `src/Api/Configuration/SourceConfiguration.cs`
- Modify: `src/Api/AppConfiguration.cs`
- Test: `test/Api.Tests/When_SourceConfigurationResolves.cs`

- [ ] **Step 1: Write failing tests**

Add tests that show:

```csharp
using KnowledgeSearch;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_SourceConfigurationResolves : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public When_SourceConfigurationResolves()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Then_SourcesJsonOverridesKnowledgeDirs()
    {
        var repositoryDir = Directory.CreateDirectory(Path.Combine(_tempDir, "repo")).FullName;
        File.WriteAllText(Path.Combine(_tempDir, "sources.json"), $$"""
        {
          "version": 1,
          "sources": [
            {
              "id": "repo",
              "name": "repo",
              "kind": "repository",
              "hostPath": "{{JsonPath(repositoryDir)}}"
            }
          ]
        }
        """);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KnowledgeDirs"] = Path.Combine(_tempDir, "legacy"),
                ["SourcesConfig"] = Path.Combine(_tempDir, "sources.json"),
            })
            .Build();

        var sources = AppConfiguration.ResolveSources(configuration, _ => null);

        sources.KnowledgeRoots.ShouldBe([Path.GetFullPath(repositoryDir)]);
        sources.Sources.Single().Kind.ShouldBe(SourceKind.Repository);
        sources.Sources.Single().IndexCode.ShouldBeTrue();
        sources.Sources.Single().IndexDocs.ShouldBeTrue();
    }

    [Fact]
    public void Then_KnowledgeDirsFallbackCreatesKnowledgeSources()
    {
        var first = Directory.CreateDirectory(Path.Combine(_tempDir, "docs-a")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(_tempDir, "docs-b")).FullName;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourcesConfig"] = Path.Combine(_tempDir, "missing.json"),
                ["KnowledgeDirs"] = $"{first};{second}",
            })
            .Build();

        var sources = AppConfiguration.ResolveSources(configuration, _ => null);

        sources.KnowledgeRoots.ShouldBe([Path.GetFullPath(first), Path.GetFullPath(second)]);
        sources.Sources.Select(source => source.Kind).ShouldAllBe(kind => kind == SourceKind.Knowledge);
        sources.Sources.ShouldAllBe(source => source.IndexDocs && !source.IndexCode);
    }

    [Fact]
    public void Then_RepositorySourcesUseDefaultIncludesAndExcludes()
    {
        var repositoryDir = Directory.CreateDirectory(Path.Combine(_tempDir, "orders-ms")).FullName;
        File.WriteAllText(Path.Combine(_tempDir, "sources.json"), $$"""
        {
          "version": 1,
          "sources": [
            {
              "id": "orders-ms",
              "name": "orders-ms",
              "kind": "repository",
              "hostPath": "{{JsonPath(repositoryDir)}}"
            }
          ]
        }
        """);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourcesConfig"] = Path.Combine(_tempDir, "sources.json"),
            })
            .Build();

        var source = AppConfiguration.ResolveSources(configuration, _ => null).Sources.Single();

        source.DocIncludes.ShouldContain("README.md");
        source.DocIncludes.ShouldContain("docs/**/*.md");
        source.DocIncludes.ShouldContain("specs/**/*.md");
        source.Excludes.ShouldContain("**/node_modules/**");
        source.Excludes.ShouldContain("**/obj/**");
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    private static string JsonPath(string path) => path.Replace("\\", "\\\\");
}
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter SourceConfiguration
```

Expected: build fails because `ResolveSources`, `SourceKind`, and source records do not exist.

- [ ] **Step 3: Implement source records and loader**

Add `SourceKind`, `ConfiguredSource`, `SourceConfigurationFile`, `ResolvedSourceConfiguration`, and default include/exclude constants. Update `AppConfiguration` with `ResolveSources()` and keep `ResolveKnowledgeRoots()` as a compatibility wrapper.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter SourceConfiguration
```

Expected: Source configuration tests pass.

---

### Task 2: Wire Program to Resolved Sources

**Files:**
- Modify: `src/Api/Program.cs`
- Modify: `src/Api/Models/Models.cs`
- Test: `test/Api.Tests/When_AppConfigurationResolves.cs`

- [ ] **Step 1: Update Program**

Replace `ResolveKnowledgeRoots()` usage with `ResolveSources()` and pass `sourceConfiguration.KnowledgeRoots` into `DbService` and `WatcherService`.

- [ ] **Step 2: Add JSON source generation metadata**

Register `SourceConfigurationFile`, `ConfiguredSource`, and `List<ConfiguredSource>` in `AppJsonContext`.

- [ ] **Step 3: Run existing configuration tests**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter AppConfiguration
```

Expected: existing compatibility test passes.

---

### Task 3: Full Verification

**Files:**
- No new files.

- [ ] **Step 1: Run all tests**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal
```

Expected: all tests pass.

- [ ] **Step 2: Inspect diff**

Run:

```powershell
git diff --stat
git diff -- src/Api test/Api.Tests docs/superpowers/plans/2026-05-16-source-configuration.md
```

Expected: changes are limited to source configuration, Program wiring, tests, and this plan.

- [ ] **Step 3: Commit**

Run:

```powershell
git add docs/superpowers/plans/2026-05-16-source-configuration.md src/Api test/Api.Tests
git commit -m "feat: add source configuration"
```

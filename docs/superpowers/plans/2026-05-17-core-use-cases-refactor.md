# Core Use Cases Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce a reusable `Core` project with result-based use cases and domain behavior, starting with Sources so API endpoints become thin HTTP adapters.

**Architecture:** `src/Core` owns application contracts, domain models, use cases, and infrastructure abstractions. `src/Api` owns ASP.NET endpoints, concrete file/SQLite implementations, JSON source generation, and HTTP result mapping. Core must not reference ASP.NET, SQLite, Podman, or direct filesystem APIs except through abstractions.

**Tech Stack:** .NET 10, C#, xUnit, Shouldly, ASP.NET Core Minimal API, SQLite implementations remaining in `src/Api` for this phase.

---

## Boundaries

Core can depend on:
- BCL types such as `Task`, `CancellationToken`, `IReadOnlyList`, `Path` only for pure path string manipulation.
- Its own abstractions and domain types.

Core must not depend on:
- `Microsoft.AspNetCore.*`
- `Microsoft.Data.Sqlite`
- `Microsoft.Extensions.Configuration`
- concrete `File`, `Directory`, `HttpContext`, `IResult`, `Results`
- Podman scripts or container path discovery from runtime globals

API can depend on Core and infrastructure packages.

---

## File Structure

Create:
- `src/Core/Core.csproj`  
  Defines the reusable project and root namespace.
- `src/Core/Common/Error.cs`  
  Defines `ErrorKind` and `Error`.
- `src/Core/Common/Result.cs`  
  Defines `Result` and `Result<T>`.
- `src/Core/UseCases/IUseCase.cs`  
  Defines the application use case contract.
- `src/Core/Domain/Sources/SourceKind.cs`  
  Moves source kind out of API.
- `src/Core/Domain/Sources/ConfiguredSource.cs`  
  Moves source behavior and defaults out of API.
- `src/Core/Domain/Sources/SourceConfigurationFile.cs`  
  Holds persisted source config DTOs and conversion into domain.
- `src/Core/Abstractions/Sources/ISourceConfigurationStore.cs`  
  Abstracts reading/writing/exporting source config.
- `src/Core/Abstractions/Search/IDocumentIndex.cs`  
  Abstracts doc root updates needed by source save.
- `src/Core/Abstractions/CodeGraph/ICodeGraphScanner.cs`  
  Abstracts repo scanning needed by source save.
- `src/Core/Abstractions/Files/IFileSystem.cs`  
  Abstracts `Directory.Exists` needed by source save.
- `src/Core/UseCases/Sources/GetSourcesUseCase.cs`
- `src/Core/UseCases/Sources/SaveSourcesUseCase.cs`
- `src/Core/UseCases/Sources/ExportSourcesUseCase.cs`
- `src/Api/Endpoints/HttpResultMapper.cs`  
  Converts Core `Result` into ASP.NET `IResult`.
- `test/Api.Tests/When_ResultIsCreated.cs`
- `test/Api.Tests/When_ConfiguredSourceIsCreated.cs`
- `test/Api.Tests/When_SaveSourcesUseCaseExecutes.cs`

Modify:
- `knowledge-search.slnx`
- `src/Api/Api.csproj`
- `test/Api.Tests/Api.Tests.csproj`
- `src/Api/Configuration/SourceConfiguration.cs`
- `src/Api/Configuration/ISourceConfigurationService.cs`
- `src/Api/Configuration/SourceConfigurationService.cs`
- `src/Api/Endpoints/SourcesEndpoints.cs`
- `src/Api/Program.cs`
- `src/Api/Models/Models.cs`
- `src/Api/Persistence/IDbService.cs`
- `src/Api/CodeGraph/ICodeGraphService.cs`

---

## Task 1: Create Core Project And Wire References

**Files:**
- Create: `src/Core/Core.csproj`
- Modify: `knowledge-search.slnx`
- Modify: `src/Api/Api.csproj`
- Modify: `test/Api.Tests/Api.Tests.csproj`

- [ ] **Step 1: Create `src/Core/Core.csproj`**

Use the repo-wide `Directory.Build.props` for `TargetFramework`, nullable, implicit usings, language version, analyzers, and warnings-as-errors. The Core project should only declare what is different from the shared defaults.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>KnowledgeSearch.Core</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Add Core to the solution**

Run:

```powershell
dotnet sln knowledge-search.slnx add src\Core\Core.csproj
```

Expected: solution adds `src/Core/Core.csproj`.

- [ ] **Step 3: Reference Core from Api**

Add to `src/Api/Api.csproj`:

```xml
  <ItemGroup>
    <ProjectReference Include="../Core/Core.csproj" />
  </ItemGroup>
```

Keep the existing package references and `InternalsVisibleTo` attributes.

- [ ] **Step 4: Reference Core from tests**

Add to `test/Api.Tests/Api.Tests.csproj`:

```xml
  <ItemGroup>
    <ProjectReference Include="../../src/Core/Core.csproj" />
  </ItemGroup>
```

Keep the existing API project reference because current endpoint/infrastructure tests still target `src/Api`.

- [ ] **Step 5: Verify project wiring**

Run:

```powershell
dotnet build src\Api\Api.csproj --no-restore -v minimal
```

Expected: build succeeds or only shows existing warnings unrelated to Core.

---

## Task 2: Add Result Pattern To Core

**Files:**
- Create: `src/Core/Common/Error.cs`
- Create: `src/Core/Common/Result.cs`
- Test: `test/Api.Tests/When_ResultIsCreated.cs`

- [ ] **Step 1: Write Result tests**

Create `test/Api.Tests/When_ResultIsCreated.cs`:

```csharp
using KnowledgeSearch.Core.Common;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_ResultIsCreated
{
    [Fact]
    public void Then_SuccessResultHasNoError()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Then_ValidationResultHasValidationError()
    {
        var result = Result.Validation("Cada source requiere id, name y hostPath.");

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe("validation");
        result.Error.Message.ShouldBe("Cada source requiere id, name y hostPath.");
    }

    [Fact]
    public void Then_GenericSuccessCarriesValue()
    {
        var result = Result.Success(new[] { "Documentation" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(["Documentation"]);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter When_ResultIsCreated
```

Expected: fail because `KnowledgeSearch.Core.Common.Result` does not exist.

- [ ] **Step 3: Implement `Error`**

Create `src/Core/Common/Error.cs`:

```csharp
namespace KnowledgeSearch.Core.Common;

public enum ErrorKind
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
}

public sealed record Error(
    string Code,
    string Message,
    ErrorKind Kind);
```

- [ ] **Step 4: Implement `Result`**

Create `src/Core/Common/Result.cs`:

```csharp
namespace KnowledgeSearch.Core.Common;

public class Result
{
    private protected Result(Error? error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    public static Result Success() => new(null);

    public static Result Failure(string code, string message) =>
        new(new Error(code, message, ErrorKind.Failure));

    public static Result Validation(string message, string code = "validation") =>
        new(new Error(code, message, ErrorKind.Validation));

    public static Result NotFound(string message, string code = "not_found") =>
        new(new Error(code, message, ErrorKind.NotFound));

    public static Result Conflict(string message, string code = "conflict") =>
        new(new Error(code, message, ErrorKind.Conflict));

    public static Result Unauthorized(string message, string code = "unauthorized") =>
        new(new Error(code, message, ErrorKind.Unauthorized));

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
}

public sealed class Result<T> : Result
{
    private Result(T? value, Error? error) : base(error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(value, null);

    public new static Result<T> Failure(string code, string message) =>
        new(default, new Error(code, message, ErrorKind.Failure));

    public new static Result<T> Validation(string message, string code = "validation") =>
        new(default, new Error(code, message, ErrorKind.Validation));

    public new static Result<T> NotFound(string message, string code = "not_found") =>
        new(default, new Error(code, message, ErrorKind.NotFound));

    public new static Result<T> Conflict(string message, string code = "conflict") =>
        new(default, new Error(code, message, ErrorKind.Conflict));

    public new static Result<T> Unauthorized(string message, string code = "unauthorized") =>
        new(default, new Error(code, message, ErrorKind.Unauthorized));
}
```

`Result` and `Result<T>` must stay immutable: no public setters, no `init` setters, and no mutable collections. Values are assigned only through constructors invoked by static factory methods.

- [ ] **Step 5: Verify Result tests pass**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter When_ResultIsCreated
```

Expected: 3 tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/Core/Common/Error.cs src/Core/Common/Result.cs src/Core/Core.csproj src/Api/Api.csproj test/Api.Tests/Api.Tests.csproj knowledge-search.slnx test/Api.Tests/When_ResultIsCreated.cs
git commit -m "feat: add core result primitives"
```

---

## Task 3: Move Source Domain Into Core

**Files:**
- Create: `src/Core/Domain/Sources/SourceKind.cs`
- Create: `src/Core/Domain/Sources/SourceConfigurationFile.cs`
- Create: `src/Core/Domain/Sources/ConfiguredSource.cs`
- Modify: `src/Api/Configuration/SourceConfiguration.cs`
- Modify: `src/Api/Models/Models.cs`
- Test: `test/Api.Tests/When_ConfiguredSourceIsCreated.cs`

- [ ] **Step 1: Write domain tests**

Create `test/Api.Tests/When_ConfiguredSourceIsCreated.cs`:

```csharp
using KnowledgeSearch.Core.Domain.Sources;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_ConfiguredSourceIsCreated
{
    [Fact]
    public void Then_RepositoryDefaultsIndexCodeAndDocs()
    {
        var source = ConfiguredSource.Create(
            id: "DevHub",
            name: "DevHub",
            kind: SourceKind.Repository,
            hostPath: @"D:\DevHub");

        source.IndexCode.ShouldBeTrue();
        source.IndexDocs.ShouldBeTrue();
        source.DocIncludes.ShouldContain("README.md");
        source.DocIncludes.ShouldContain("docs/**/*.md");
        source.Excludes.ShouldContain("**/node_modules/**");
    }

    [Fact]
    public void Then_KnowledgeDefaultsDocsOnly()
    {
        var source = ConfiguredSource.Create(
            id: "docs",
            name: "docs",
            kind: SourceKind.Knowledge,
            hostPath: @"D:\Documentation");

        source.IndexCode.ShouldBeFalse();
        source.IndexDocs.ShouldBeTrue();
        source.DocIncludes.ShouldBe(["**/*.md", "**/*.mdx"]);
    }

    [Fact]
    public void Then_SourceDefinitionNormalizesMissingIdAndName()
    {
        var definition = new SourceDefinition(
            Id: "",
            Name: "",
            Kind: SourceKind.Repository,
            HostPath: @"D:\DevHub",
            IndexCode: null,
            IndexDocs: null,
            DocIncludes: null,
            CodeIncludes: null,
            Excludes: null);

        var source = definition.ToConfiguredSource();

        source.Id.ShouldBe("DevHub");
        source.Name.ShouldBe("DevHub");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter When_ConfiguredSourceIsCreated
```

Expected: fail because source domain types do not exist in Core.

- [ ] **Step 3: Move `SourceKind`**

Create `src/Core/Domain/Sources/SourceKind.cs`:

```csharp
using System.Text.Json.Serialization;

namespace KnowledgeSearch.Core.Domain.Sources;

[JsonConverter(typeof(JsonStringEnumConverter<SourceKind>))]
public enum SourceKind
{
    Knowledge,
    Repository,
}
```

- [ ] **Step 4: Move source configuration DTOs**

Create `src/Core/Domain/Sources/SourceConfigurationFile.cs`:

```csharp
namespace KnowledgeSearch.Core.Domain.Sources;

public sealed record SourceConfigurationFile(
    int Version,
    IReadOnlyList<SourceDefinition> Sources);

public sealed record SourceDefinition(
    string Id,
    string Name,
    SourceKind Kind,
    string HostPath,
    bool? IndexCode,
    bool? IndexDocs,
    IReadOnlyList<string>? DocIncludes,
    IReadOnlyList<string>? CodeIncludes,
    IReadOnlyList<string>? Excludes)
{
    public ConfiguredSource ToConfiguredSource()
    {
        return ConfiguredSource.Create(
            string.IsNullOrWhiteSpace(Id) ? ConfiguredSource.CreateId(HostPath) : Id,
            string.IsNullOrWhiteSpace(Name) ? Path.GetFileName(Path.TrimEndingDirectorySeparator(HostPath)) : Name,
            Kind,
            HostPath,
            IndexCode,
            IndexDocs,
            DocIncludes is { Count: > 0 } ? DocIncludes : null,
            CodeIncludes is { Count: > 0 } ? CodeIncludes : null,
            Excludes is { Count: > 0 } ? Excludes : null);
    }

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
}

public sealed record ResolvedSourceConfiguration(
    IReadOnlyList<ConfiguredSource> Sources,
    IReadOnlyList<string> KnowledgeRoots);
```

- [ ] **Step 5: Move source behavior**

Create `src/Core/Domain/Sources/ConfiguredSource.cs` by moving the existing `ConfiguredSource` record from `src/Api/Configuration/SourceConfiguration.cs`, changing `internal` to `public` for the record and members used outside Core.

The public surface must include:

```csharp
public sealed record ConfiguredSource(
    string Id,
    string Name,
    SourceKind Kind,
    string HostPath,
    bool IndexCode,
    bool IndexDocs,
    IReadOnlyList<string> DocIncludes,
    IReadOnlyList<string> CodeIncludes,
    IReadOnlyList<string> Excludes)
{
    public static ConfiguredSource Create(...);
    public static string CreateId(string path);
    public string GetAccessiblePath();
    public static string GetContainerSourcePath(string id);
}
```

Keep the default include/exclude lists and the Windows-to-container source mapping behavior.

- [ ] **Step 6: Replace API source config file with imports**

Delete the old type definitions from `src/Api/Configuration/SourceConfiguration.cs` and replace the file with:

```csharp
using KnowledgeSearch.Core.Domain.Sources;
```

If the empty shim file causes analyzer noise, delete `src/Api/Configuration/SourceConfiguration.cs` and add `using KnowledgeSearch.Core.Domain.Sources;` to each API file that uses source types.

- [ ] **Step 7: Update API JSON context**

Modify `src/Api/Models/Models.cs` to include:

```csharp
using KnowledgeSearch.Core.Domain.Sources;
```

Keep existing `[JsonSerializable]` attributes for `SourceConfigurationFile`, `SourceDefinition`, `ConfiguredSource`, and source lists.

- [ ] **Step 8: Verify domain tests pass**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter When_ConfiguredSourceIsCreated
```

Expected: 3 tests pass.

- [ ] **Step 9: Run existing source tests**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter "SourceConfiguration|SourceEndpoints"
```

Expected: existing source tests pass.

- [ ] **Step 10: Commit**

```powershell
git add src/Core/Domain/Sources src/Api/Configuration/SourceConfiguration.cs src/Api/Models/Models.cs test/Api.Tests/When_ConfiguredSourceIsCreated.cs
git commit -m "refactor: move source domain to core"
```

---

## Task 4: Add Source Use Case Abstractions

**Files:**
- Create: `src/Core/UseCases/IUseCase.cs`
- Create: `src/Core/Abstractions/Sources/ISourceConfigurationStore.cs`
- Create: `src/Core/Abstractions/Search/IDocumentIndex.cs`
- Create: `src/Core/Abstractions/CodeGraph/ICodeGraphScanner.cs`
- Create: `src/Core/Abstractions/Files/IFileSystem.cs`

- [ ] **Step 1: Add use case interface**

Create `src/Core/UseCases/IUseCase.cs`:

```csharp
using KnowledgeSearch.Core.Common;

namespace KnowledgeSearch.Core.UseCases;

public interface IUseCase<in TCommand, TResponse>
{
    Task<Result<TResponse>> ExecuteAsync(TCommand command, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Add source configuration store abstraction**

Create `src/Core/Abstractions/Sources/ISourceConfigurationStore.cs`:

```csharp
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Sources;

namespace KnowledgeSearch.Core.Abstractions.Sources;

public interface ISourceConfigurationStore
{
    SourceConfigurationFile GetConfiguration();
    IReadOnlyList<string> GetKnowledgeRootNames();
    string ExportJson();
    Result Save(SourceConfigurationFile configuration);
}
```

- [ ] **Step 3: Add document index abstraction**

Create `src/Core/Abstractions/Search/IDocumentIndex.cs`:

```csharp
namespace KnowledgeSearch.Core.Abstractions.Search;

public interface IDocumentIndex
{
    void UpdateRoots(IReadOnlyList<string> roots);
}
```

- [ ] **Step 4: Add code graph scanner abstraction**

Create `src/Core/Abstractions/CodeGraph/ICodeGraphScanner.cs`:

```csharp
namespace KnowledgeSearch.Core.Abstractions.CodeGraph;

public interface ICodeGraphScanner
{
    void ScanDirectory(string directoryPath);
}
```

- [ ] **Step 5: Add filesystem abstraction**

Create `src/Core/Abstractions/Files/IFileSystem.cs`:

```csharp
namespace KnowledgeSearch.Core.Abstractions.Files;

public interface IFileSystem
{
    bool DirectoryExists(string path);
}
```

- [ ] **Step 6: Verify build**

Run:

```powershell
dotnet build src\Core\Core.csproj --no-restore -v minimal
```

Expected: Core builds with no ASP.NET or SQLite dependencies.

- [ ] **Step 7: Commit**

```powershell
git add src/Core/UseCases src/Core/Abstractions
git commit -m "feat: add core use case abstractions"
```

---

## Task 5: Implement Sources Use Cases

**Files:**
- Create: `src/Core/UseCases/Sources/GetSourcesUseCase.cs`
- Create: `src/Core/UseCases/Sources/ExportSourcesUseCase.cs`
- Create: `src/Core/UseCases/Sources/SaveSourcesUseCase.cs`
- Test: `test/Api.Tests/When_SaveSourcesUseCaseExecutes.cs`

- [ ] **Step 1: Write SaveSources use case tests**

Create `test/Api.Tests/When_SaveSourcesUseCaseExecutes.cs`:

```csharp
using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Abstractions.Sources;
using KnowledgeSearch.Core.Domain.Sources;
using KnowledgeSearch.Core.UseCases.Sources;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_SaveSourcesUseCaseExecutes
{
    [Fact]
    public async Task Then_ItUpdatesDocRootsAndScansCodeSources()
    {
        var configuration = new SourceConfigurationFile(1, [
            new SourceDefinition(
                "docs",
                "docs",
                SourceKind.Knowledge,
                @"D:\Docs",
                IndexCode: false,
                IndexDocs: true,
                DocIncludes: null,
                CodeIncludes: null,
                Excludes: null),
            new SourceDefinition(
                "repo",
                "repo",
                SourceKind.Repository,
                @"D:\Repo",
                IndexCode: true,
                IndexDocs: false,
                DocIncludes: null,
                CodeIncludes: null,
                Excludes: null),
        ]);

        var store = new Mock<ISourceConfigurationStore>();
        var documentIndex = new Mock<IDocumentIndex>();
        var codeScanner = new Mock<ICodeGraphScanner>();
        var fileSystem = new Mock<IFileSystem>();

        store.Setup(s => s.Save(configuration)).Returns(KnowledgeSearch.Core.Common.Result.Success());
        store.Setup(s => s.GetConfiguration()).Returns(configuration);
        fileSystem.Setup(fs => fs.DirectoryExists(@"D:\Repo")).Returns(true);

        var useCase = new SaveSourcesUseCase(
            store.Object,
            documentIndex.Object,
            codeScanner.Object,
            fileSystem.Object);

        var result = await useCase.ExecuteAsync(new SaveSourcesCommand(configuration), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Configuration.ShouldBe(configuration);
        documentIndex.Verify(i => i.UpdateRoots(It.Is<IReadOnlyList<string>>(roots =>
            roots.Count == 1 && roots[0] == @"D:\Docs")), Times.Once);
        codeScanner.Verify(s => s.ScanDirectory(@"D:\Repo"), Times.Once);
    }

    [Fact]
    public async Task Then_ItReturnsValidationFailureFromStore()
    {
        var configuration = new SourceConfigurationFile(1, []);
        var store = new Mock<ISourceConfigurationStore>();
        var documentIndex = new Mock<IDocumentIndex>();
        var codeScanner = new Mock<ICodeGraphScanner>();
        var fileSystem = new Mock<IFileSystem>();

        store.Setup(s => s.Save(configuration))
            .Returns(KnowledgeSearch.Core.Common.Result.Validation("id duplicado: docs"));

        var useCase = new SaveSourcesUseCase(
            store.Object,
            documentIndex.Object,
            codeScanner.Object,
            fileSystem.Object);

        var result = await useCase.ExecuteAsync(new SaveSourcesCommand(configuration), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldBe("id duplicado: docs");
        documentIndex.Verify(i => i.UpdateRoots(It.IsAny<IReadOnlyList<string>>()), Times.Never);
        codeScanner.Verify(s => s.ScanDirectory(It.IsAny<string>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter When_SaveSourcesUseCaseExecutes
```

Expected: fail because `SaveSourcesUseCase` does not exist.

- [ ] **Step 3: Implement GetSources use case**

Create `src/Core/UseCases/Sources/GetSourcesUseCase.cs`:

```csharp
using KnowledgeSearch.Core.Abstractions.Sources;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Sources;

namespace KnowledgeSearch.Core.UseCases.Sources;

public sealed record GetSourcesCommand;

public sealed record GetSourcesResponse(SourceConfigurationFile Configuration);

public sealed class GetSourcesUseCase(ISourceConfigurationStore store)
    : IUseCase<GetSourcesCommand, GetSourcesResponse>
{
    public Task<Result<GetSourcesResponse>> ExecuteAsync(
        GetSourcesCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new GetSourcesResponse(store.GetConfiguration())));
    }
}
```

- [ ] **Step 4: Implement ExportSources use case**

Create `src/Core/UseCases/Sources/ExportSourcesUseCase.cs`:

```csharp
using KnowledgeSearch.Core.Abstractions.Sources;
using KnowledgeSearch.Core.Common;

namespace KnowledgeSearch.Core.UseCases.Sources;

public sealed record ExportSourcesCommand;

public sealed record ExportSourcesResponse(string Json);

public sealed class ExportSourcesUseCase(ISourceConfigurationStore store)
    : IUseCase<ExportSourcesCommand, ExportSourcesResponse>
{
    public Task<Result<ExportSourcesResponse>> ExecuteAsync(
        ExportSourcesCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new ExportSourcesResponse(store.ExportJson())));
    }
}
```

- [ ] **Step 5: Implement SaveSources use case**

Create `src/Core/UseCases/Sources/SaveSourcesUseCase.cs`:

```csharp
using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Abstractions.Sources;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Sources;

namespace KnowledgeSearch.Core.UseCases.Sources;

public sealed record SaveSourcesCommand(SourceConfigurationFile Configuration);

public sealed record SaveSourcesResponse(SourceConfigurationFile Configuration);

public sealed class SaveSourcesUseCase(
    ISourceConfigurationStore store,
    IDocumentIndex documentIndex,
    ICodeGraphScanner codeGraphScanner,
    IFileSystem fileSystem)
    : IUseCase<SaveSourcesCommand, SaveSourcesResponse>
{
    public Task<Result<SaveSourcesResponse>> ExecuteAsync(
        SaveSourcesCommand command,
        CancellationToken cancellationToken)
    {
        var saveResult = store.Save(command.Configuration);
        if (saveResult.IsFailure)
        {
            return Task.FromResult(Result<SaveSourcesResponse>.Validation(
                saveResult.Error!.Message,
                saveResult.Error.Code));
        }

        var savedConfig = store.GetConfiguration();
        var configuredSources = savedConfig.Sources
            .Select(source => source.ToConfiguredSource())
            .ToArray();

        var docRoots = configuredSources
            .Where(source => source.IndexDocs)
            .Select(source => source.GetAccessiblePath())
            .ToArray();

        documentIndex.UpdateRoots(docRoots);

        foreach (var source in configuredSources.Where(source => source.IndexCode))
        {
            var accessiblePath = source.GetAccessiblePath();
            if (fileSystem.DirectoryExists(accessiblePath))
            {
                codeGraphScanner.ScanDirectory(accessiblePath);
            }
        }

        return Task.FromResult(Result.Success(new SaveSourcesResponse(savedConfig)));
    }
}
```

- [ ] **Step 6: Verify SaveSources use case tests pass**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter When_SaveSourcesUseCaseExecutes
```

Expected: 2 tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/Core/UseCases/Sources test/Api.Tests/When_SaveSourcesUseCaseExecutes.cs
git commit -m "feat: add source use cases"
```

---

## Task 6: Adapt API Infrastructure To Core Abstractions

**Files:**
- Modify: `src/Api/Configuration/ISourceConfigurationService.cs`
- Modify: `src/Api/Configuration/SourceConfigurationService.cs`
- Modify: `src/Api/Persistence/IDbService.cs`
- Modify: `src/Api/Persistence/DbService.cs`
- Modify: `src/Api/CodeGraph/ICodeGraphService.cs`
- Modify: `src/Api/CodeGraph/CodeGraphService.cs`
- Create: `src/Api/Services/FileSystemAdapter.cs`
- Modify: `src/Api/Program.cs`

- [ ] **Step 1: Make source configuration service implement Core store**

Modify `src/Api/Configuration/ISourceConfigurationService.cs`:

```csharp
using KnowledgeSearch.Core.Abstractions.Sources;

namespace KnowledgeSearch;

internal interface ISourceConfigurationService : ISourceConfigurationStore
{
}
```

- [ ] **Step 2: Update SourceConfigurationService Save return type**

Modify `src/Api/Configuration/SourceConfigurationService.cs` so `Save` returns Core `Result`:

```csharp
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Sources;
```

Replace:

```csharp
public SaveSourcesResult Save(SourceConfigurationFile configuration)
```

with:

```csharp
public Result Save(SourceConfigurationFile configuration)
```

Replace bad validation return:

```csharp
return new SaveSourcesResult(false, validationError);
```

with:

```csharp
return Result.Validation(validationError);
```

Replace success return:

```csharp
return new SaveSourcesResult(true, null);
```

with:

```csharp
return Result.Success();
```

Keep `SaveSourcesResult` only if other API JSON responses still need it; otherwise remove it in a later cleanup task.

- [ ] **Step 3: Make DbService implement document index abstraction**

Modify `src/Api/Persistence/IDbService.cs`:

```csharp
using KnowledgeSearch.Core.Abstractions.Search;

namespace KnowledgeSearch;

internal interface IDbService : IDocumentIndex
{
    // keep existing members
}
```

`DbService` already has `UpdateRoots(IReadOnlyList<string> roots)`, so implementation should compile without method changes.

- [ ] **Step 4: Make CodeGraphService implement scanner abstraction**

Modify `src/Api/CodeGraph/ICodeGraphService.cs`:

```csharp
using KnowledgeSearch.Core.Abstractions.CodeGraph;

namespace KnowledgeSearch;

internal interface ICodeGraphService : ICodeGraphScanner
{
    // keep existing members
}
```

`CodeGraphService` already has `ScanDirectory(string directoryPath)`.

- [ ] **Step 5: Add filesystem adapter**

Create `src/Api/Services/FileSystemAdapter.cs`:

```csharp
using KnowledgeSearch.Core.Abstractions.Files;

namespace KnowledgeSearch;

internal sealed class FileSystemAdapter : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
}
```

- [ ] **Step 6: Register Core use cases and adapters**

Modify `src/Api/Program.cs`:

```csharp
using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Abstractions.Sources;
using KnowledgeSearch.Core.UseCases.Sources;
```

After existing service registrations, add:

```csharp
builder.Services.AddSingleton<ISourceConfigurationStore>(sourceConfigService);
builder.Services.AddSingleton<IDocumentIndex>(serviceProvider => serviceProvider.GetRequiredService<IDbService>());
builder.Services.AddSingleton<ICodeGraphScanner>(serviceProvider => serviceProvider.GetRequiredService<ICodeGraphService>());
builder.Services.AddSingleton<IFileSystem, FileSystemAdapter>();
builder.Services.AddSingleton<GetSourcesUseCase>();
builder.Services.AddSingleton<SaveSourcesUseCase>();
builder.Services.AddSingleton<ExportSourcesUseCase>();
```

Ensure `ICodeGraphService` is registered before resolving `ICodeGraphScanner`.

- [ ] **Step 7: Verify build**

Run:

```powershell
dotnet build src\Api\Api.csproj --no-restore -v minimal
```

Expected: build passes.

- [ ] **Step 8: Commit**

```powershell
git add src/Api/Configuration src/Api/Persistence src/Api/CodeGraph src/Api/Services/FileSystemAdapter.cs src/Api/Program.cs
git commit -m "refactor: adapt api services to core abstractions"
```

---

## Task 7: Make Sources Endpoints Thin HTTP Adapters

**Files:**
- Create: `src/Api/Endpoints/HttpResultMapper.cs`
- Modify: `src/Api/Endpoints/SourcesEndpoints.cs`
- Test: existing `test/Api.Tests/When_SourceEndpointsAreRequested.cs`

- [ ] **Step 1: Add HTTP result mapper**

Create `src/Api/Endpoints/HttpResultMapper.cs`:

```csharp
using KnowledgeSearch.Core.Common;

namespace KnowledgeSearch;

internal static class HttpResultMapper
{
    public static IResult ToHttpResult<T>(
        this Result<T> result,
        Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess(result.Value!);
        }

        var error = result.Error!;
        return error.Kind switch
        {
            ErrorKind.Validation => Results.BadRequest(new ErrorResult(error.Message)),
            ErrorKind.NotFound => Results.NotFound(new ErrorResult(error.Message)),
            ErrorKind.Conflict => Results.Conflict(new ErrorResult(error.Message)),
            ErrorKind.Unauthorized => Results.Unauthorized(),
            _ => Results.BadRequest(new ErrorResult(error.Message)),
        };
    }
}
```

- [ ] **Step 2: Rewrite SourcesEndpoints**

Modify `src/Api/Endpoints/SourcesEndpoints.cs`:

```csharp
using KnowledgeSearch.Core.Domain.Sources;
using KnowledgeSearch.Core.UseCases.Sources;

namespace KnowledgeSearch;

internal static class SourcesEndpoints
{
    public static void MapSourcesRoutes(this WebApplication app)
    {
        app.MapGet("/sources", async (
            GetSourcesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new GetSourcesCommand(), cancellationToken);
            return result.ToHttpResult(response => Results.Ok(response.Configuration));
        });

        app.MapPut("/sources", async (
            SourceConfigurationFile request,
            SaveSourcesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new SaveSourcesCommand(request), cancellationToken);
            return result.ToHttpResult(response => Results.Ok(response.Configuration));
        });

        app.MapGet("/sources/export", async (
            ExportSourcesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new ExportSourcesCommand(), cancellationToken);
            return result.ToHttpResult(response =>
                Results.Text(response.Json, "application/json; charset=utf-8"));
        });
    }
}
```

The endpoint must not call `IDbService`, `ICodeGraphService`, `Directory.Exists`, or `ISourceConfigurationService` directly.

- [ ] **Step 3: Verify endpoint tests pass**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal --filter SourceEndpoints
```

Expected: endpoint tests pass.

- [ ] **Step 4: Verify no business orchestration remains in SourcesEndpoints**

Run:

```powershell
rg "UpdateRoots|ScanDirectory|Directory\\.Exists|ToConfiguredSource" src\Api\Endpoints\SourcesEndpoints.cs
```

Expected: no matches.

- [ ] **Step 5: Commit**

```powershell
git add src/Api/Endpoints/HttpResultMapper.cs src/Api/Endpoints/SourcesEndpoints.cs
git commit -m "refactor: route source endpoints through use cases"
```

---

## Task 8: Full Verification

**Files:**
- No new files.

- [ ] **Step 1: Run Core build**

Run:

```powershell
dotnet build src\Core\Core.csproj --no-restore -v minimal
```

Expected: build passes.

- [ ] **Step 2: Run API build**

Run:

```powershell
dotnet build src\Api\Api.csproj --no-restore -v minimal
```

Expected: build passes.

- [ ] **Step 3: Run backend tests**

Run:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal
```

Expected: all tests pass. Existing SQLite version warnings are acceptable if they match prior runs.

- [ ] **Step 4: Run source-focused e2e if frontend/API behavior changed**

If endpoint responses changed only structurally equivalent JSON, skip e2e. If any response shape changed, run:

```powershell
$env:CI='1'; pnpm --dir e2e run test
```

Expected: 13 tests pass.

- [ ] **Step 5: Inspect Core dependencies**

Run:

```powershell
rg "AspNetCore|Sqlite|HttpContext|IResult|Results\\.|File\\.|Directory\\." src\Core
```

Expected: no matches.

- [ ] **Step 6: Inspect endpoint thinness**

Run:

```powershell
rg "UpdateRoots|ScanDirectory|Directory\\.Exists|File\\.WriteAllText|JsonSerializer\\.Serialize" src\Api\Endpoints
```

Expected: no matches in `SourcesEndpoints.cs`; matches in other endpoint files are allowed until later phases.

- [ ] **Step 7: Commit final verification cleanup**

If verification required small fixes, stage only the files changed by those fixes and commit them with:

```powershell
git commit -m "test: cover source use case refactor"
```

Before committing, run `git status --short` and confirm no local runtime folders such as `data/`, `.claude/`, or generated `src/Api/Properties/` files are staged.

---

## Later Phases

Do not implement these in this first plan unless the Sources pilot is complete and reviewed.

1. Move Search endpoint orchestration into Core:
   - `SearchDocumentsUseCase`
   - `IndexDocumentsUseCase`
   - `GetDocumentRootsUseCase`
   - `GetFileContentUseCase`
   - `SaveFileContentUseCase`

2. Move Code Graph endpoint orchestration into Core:
   - `GetRepositoriesUseCase`
   - `ScanRepositoryUseCase`
   - `SearchCodeDocumentsUseCase`
   - `SearchCodeGraphUseCase`
   - `BuildCrossRepoRefsUseCase`

3. Move Skills endpoint orchestration into Core only after deciding whether skills are domain/application behavior or local tooling.

4. Consider a dedicated `test/Core.Tests` project once Core has enough tests that `Api.Tests` becomes noisy.

---

## Self-Review

- Spec coverage: The plan creates `Core`, introduces `Result`, `IUseCase`, domain Sources, infrastructure abstractions, and routes Sources endpoints through use cases.
- Placeholder scan: No `TBD`, `TODO`, or unspecified implementation steps remain.
- Type consistency: Commands/responses use `ExecuteAsync(TCommand, CancellationToken)` and return `Result<TResponse>` consistently.
- Scope check: This plan intentionally implements only Sources as the pilot and leaves Search/CodeGraph for later phases.

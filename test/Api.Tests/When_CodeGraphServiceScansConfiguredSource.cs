using KnowledgeSearch;
using KnowledgeSearch.Core.Domain.Sources;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Api.Tests;

// Asegura que ScanDirectory respete los Excludes definidos en sources.json
// (campo ConfiguredSource.Excludes), no solo la blocklist hardcoded.
public class When_CodeGraphServiceScansConfiguredSource : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _dbPath             = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    private CodeGraphRepository? _repository;

    public When_CodeGraphServiceScansConfiguredSource() => Directory.CreateDirectory(_temporaryDirectory);

    [Fact]
    public void Then_DoesNotIndexFilesUnderExcludeGlobFromConfiguredSource()
    {
        WriteSourceFile("src/RealService.cs",      "namespace A; public class RealService {}");
        WriteSourceFile("sandbox/Throwaway.cs",    "namespace A; public class Throwaway {}");
        WriteSourceFile("reports/q1/Stale.cs",     "namespace A; public class Stale {}");

        var source = ConfiguredSource.Create(
            id:        "test-source",
            name:      "test-source",
            kind:      SourceKind.Repository,
            hostPath:  _temporaryDirectory,
            excludes:  ["**/sandbox/**", "**/reports/**"]);

        var result = BuildService().ScanDirectory(source);

        var names = result.Nodes.Select(n => n.Name).ToList();
        names.ShouldContain("RealService");
        names.ShouldNotContain("Throwaway");
        names.ShouldNotContain("Stale");
    }

    [Fact]
    public void Then_StillExcludesDefaultDirectoriesWhenConfiguredSourceHasNoExtraExcludes()
    {
        WriteSourceFile("src/Real.cs",          "namespace A; public class Real {}");
        WriteSourceFile("node_modules/junk.cs", "namespace A; public class Junk {}");
        WriteSourceFile("bin/Compiled.cs",      "namespace A; public class Compiled {}");

        var source = ConfiguredSource.Create(
            id:       "test-source",
            name:     "test-source",
            kind:     SourceKind.Repository,
            hostPath: _temporaryDirectory);

        var result = BuildService().ScanDirectory(source);

        var names = result.Nodes.Select(n => n.Name).ToList();
        names.ShouldContain("Real");
        names.ShouldNotContain("Junk");
        names.ShouldNotContain("Compiled");
    }

    public void Dispose()
    {
        _repository?.Dispose();
        SqliteConnection.ClearAllPools();

        try { Directory.Delete(_temporaryDirectory, recursive: true); } catch { }
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        GC.SuppressFinalize(this);
    }

    private CodeGraphService BuildService()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ISourceFileParser, CSharpParser>(".cs");

        _repository?.Dispose();
        _repository = new CodeGraphRepository(_dbPath);
        return new CodeGraphService(services.BuildServiceProvider(), _repository);
    }

    private void WriteSourceFile(string relativePath, string sourceCode)
    {
        var fullPath = Path.Combine(_temporaryDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, sourceCode);
    }
}

using System.Diagnostics;
using KnowledgeSearch;
using KnowledgeSearch.Core.Domain.Sources;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

// Benchmark end-to-end de ScanDirectory contra un repo REAL del host. Lee
// el path desde la env var KNOWLEDGE_SEARCH_BENCH_REPO. Si no esta seteada,
// se skipea (no aparece en --test normal).
//
// Uso esperado: invocar via "dotnet run scripts/podman/podman-dev.cs --
// --bench <hostRepoPath>", que se encarga de montar el path y setear la
// env var dentro del container.
//
// Ver docs/benchmark.md para el flujo completo de comparacion before/after.
public class When_CodeGraphServiceBenchesRealRepository : IDisposable
{
    private const string RepoEnvVar = "KNOWLEDGE_SEARCH_BENCH_REPO";

    private readonly ITestOutputHelper _output;
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    private CodeGraphRepository? _repository;

    public When_CodeGraphServiceBenchesRealRepository(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Then_ScanDirectoryCompletesAndReportsTiming()
    {
        var repoPath = Environment.GetEnvironmentVariable(RepoEnvVar);
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            _output.WriteLine($"[skipped] Sin {RepoEnvVar} (corre via 'podman-dev.cs --bench <hostPath>').");
            return;
        }
        if (!Directory.Exists(repoPath))
        {
            _output.WriteLine($"[skipped] El path en {RepoEnvVar} no existe dentro del container: {repoPath}");
            return;
        }

        var source = ConfiguredSource.Create(
            id:       "bench",
            name:     "bench",
            kind:     SourceKind.Repository,
            hostPath: repoPath);

        var service = BuildService();

        var stopwatch = Stopwatch.StartNew();
        var result = service.ScanDirectory(source);
        stopwatch.Stop();

        _output.WriteLine($"Repo:         {repoPath}");
        _output.WriteLine($"Elapsed:      {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"FilesScanned: {result.FilesScanned}");
        _output.WriteLine($"FilesSkipped: {result.FilesSkipped}");
        _output.WriteLine($"Nodes:        {result.Nodes.Count}");
        _output.WriteLine($"Edges:        {result.Edges.Count}");
    }

    public void Dispose()
    {
        _repository?.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        GC.SuppressFinalize(this);
    }

    private CodeGraphService BuildService()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ISourceFileParser, CSharpParser>(".cs");
        foreach (var ext in new[] { ".ts", ".tsx", ".js", ".jsx", ".mjs" })
        {
            services.AddKeyedSingleton<ISourceFileParser, TypeScriptParser>(ext);
        }
        services.AddKeyedSingleton<ISourceFileParser, PythonParser>(".py");

        _repository?.Dispose();
        _repository = new CodeGraphRepository(_dbPath);
        return new CodeGraphService(services.BuildServiceProvider(), _repository);
    }
}

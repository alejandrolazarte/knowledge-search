using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Abstractions.Jobs;
using KnowledgeSearch.Core.Abstractions.Sources;
using KnowledgeSearch.Core.Domain.Sources;
using KnowledgeSearch.Core.UseCases.Sources;

namespace KnowledgeSearch;

public sealed class ScanRepositoryJob(
    string directoryPath,
    ICodeGraphSearchService graphService,
    ISourceConfigurationStore sourceConfigurationStore) : IJob
{
    public string Kind => "scan-repository";

    public Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
    {
        var configured = TryFindConfiguredSource(directoryPath);
        var result = configured is not null
            ? graphService.ScanDirectory(configured)
            : graphService.ScanDirectory(directoryPath);

        return context.ReportProgressAsync(
            $"scan {Path.GetFileName(directoryPath)}: scanned={result.FilesScanned} skipped={result.FilesSkipped} nodes={result.Nodes.Count} edges={result.Edges.Count}",
            cancellationToken);
    }

    private ConfiguredSource? TryFindConfiguredSource(string path)
    {
        var normalized = Path.TrimEndingDirectorySeparator(path);
        return sourceConfigurationStore.GetConfiguration().Sources
            .Select(s => s.ToConfiguredSource())
            .FirstOrDefault(s => string.Equals(
                Path.TrimEndingDirectorySeparator(s.GetAccessiblePath()),
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class ScanRepositoryJobFactory(
    ICodeGraphSearchService graphService,
    ISourceConfigurationStore sourceConfigurationStore) : IScanRepositoryJobFactory
{
    public IJob Create(string directoryPath) =>
        new ScanRepositoryJob(directoryPath, graphService, sourceConfigurationStore);
}

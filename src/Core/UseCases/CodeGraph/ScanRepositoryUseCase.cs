using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Abstractions.Sources;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.CodeGraph;
using KnowledgeSearch.Core.Domain.Sources;

namespace KnowledgeSearch.Core.UseCases.CodeGraph;

public sealed record ScanRepositoryCommand(string? DirectoryPath);

public sealed class ScanRepositoryUseCase(
    ICodeGraphSearchService graphService,
    IFileSystem fileSystem,
    ISourceConfigurationStore sourceConfigurationStore)
    : IUseCase<ScanRepositoryCommand, ScanRepositoryResponse>
{
    public Task<Result<ScanRepositoryResponse>> ExecuteAsync(
        ScanRepositoryCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.DirectoryPath))
        {
            return Task.FromResult<Result<ScanRepositoryResponse>>(
                Result.Validation<ScanRepositoryResponse>("El campo directoryPath es requerido."));
        }

        if (!fileSystem.DirectoryExists(command.DirectoryPath))
        {
            return Task.FromResult<Result<ScanRepositoryResponse>>(
                Result.Validation<ScanRepositoryResponse>($"El directorio no existe: {command.DirectoryPath}"));
        }

        var configuredSource = TryFindConfiguredSource(command.DirectoryPath);
        var scanResult = configuredSource is not null
            ? graphService.ScanDirectory(configuredSource)
            : graphService.ScanDirectory(command.DirectoryPath);
        var repositoryName = Path.GetFileName(command.DirectoryPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
            '\\',
            '/'));

        return Task.FromResult<Result<ScanRepositoryResponse>>(new ScanRepositoryResponse(
            repositoryName,
            scanResult.FilesScanned,
            scanResult.FilesSkipped,
            scanResult.Nodes.Count,
            scanResult.Edges.Count));
    }

    private ConfiguredSource? TryFindConfiguredSource(string directoryPath)
    {
        var normalized = Path.TrimEndingDirectorySeparator(directoryPath);
        return sourceConfigurationStore.GetConfiguration().Sources
            .Select(s => s.ToConfiguredSource())
            .FirstOrDefault(s => string.Equals(
                Path.TrimEndingDirectorySeparator(s.GetAccessiblePath()),
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }
}

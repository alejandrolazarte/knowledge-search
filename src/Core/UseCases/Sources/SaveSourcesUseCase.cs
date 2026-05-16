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
            return Task.FromResult<Result<SaveSourcesResponse>>(saveResult.Error!);
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

        return Task.FromResult<Result<SaveSourcesResponse>>(new SaveSourcesResponse(savedConfig));
    }
}

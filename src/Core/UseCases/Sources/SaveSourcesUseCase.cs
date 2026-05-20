using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Abstractions.Jobs;
using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Abstractions.Sources;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Sources;

namespace KnowledgeSearch.Core.UseCases.Sources;

public sealed record SaveSourcesCommand(SourceConfigurationFile Configuration);

public sealed record SaveSourcesResponse(SourceConfigurationFile Configuration, IReadOnlyList<Guid> JobIds);

public sealed class SaveSourcesUseCase(
    ISourceConfigurationStore store,
    IDocumentIndex documentIndex,
    IJobQueue jobQueue,
    IIndexDocumentsJobFactory indexJobFactory,
    IScanRepositoryJobFactory scanJobFactory,
    IFileSystem fileSystem)
    : IUseCase<SaveSourcesCommand, SaveSourcesResponse>
{
    public async Task<Result<SaveSourcesResponse>> ExecuteAsync(
        SaveSourcesCommand command,
        CancellationToken cancellationToken)
    {
        var saveResult = store.Save(command.Configuration);
        if (saveResult.IsFailure)
        {
            return saveResult.Error!;
        }

        var savedConfig = store.GetConfiguration();
        var configuredSources = savedConfig.Sources
            .Select(source => source.ToConfiguredSource())
            .ToArray();

        var docSources = configuredSources
            .Where(source => source.IndexDocs)
            .ToArray();

        documentIndex.UpdateSources(docSources);

        var jobIds = new List<Guid>();
        jobIds.Add(await jobQueue.EnqueueAsync(indexJobFactory.Create(), cancellationToken));

        foreach (var source in configuredSources.Where(source => source.IndexCode))
        {
            var accessiblePath = source.GetAccessiblePath();
            if (fileSystem.DirectoryExists(accessiblePath))
            {
                jobIds.Add(await jobQueue.EnqueueAsync(scanJobFactory.Create(accessiblePath), cancellationToken));
            }
        }

        return new SaveSourcesResponse(savedConfig, jobIds);
    }
}

public interface IIndexDocumentsJobFactory
{
    IJob Create();
}

public interface IScanRepositoryJobFactory
{
    IJob Create(string directoryPath);
}

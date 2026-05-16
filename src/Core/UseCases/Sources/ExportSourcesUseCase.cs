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
        return Task.FromResult<Result<ExportSourcesResponse>>(new ExportSourcesResponse(store.ExportJson()));
    }
}

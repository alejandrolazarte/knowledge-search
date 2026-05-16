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
        return Task.FromResult<Result<GetSourcesResponse>>(new GetSourcesResponse(store.GetConfiguration()));
    }
}

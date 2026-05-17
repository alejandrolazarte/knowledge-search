using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Common;

namespace KnowledgeSearch.Core.UseCases.Search;

public sealed record GetKnowledgeRootsCommand;

public sealed record GetSearchRootsResponse(IReadOnlyList<string> Roots);

public sealed class GetKnowledgeRootsUseCase(IDocumentSearchIndex searchIndex)
    : IUseCase<GetKnowledgeRootsCommand, GetSearchRootsResponse>
{
    public Task<Result<GetSearchRootsResponse>> ExecuteAsync(
        GetKnowledgeRootsCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<Result<GetSearchRootsResponse>>(
            new GetSearchRootsResponse(searchIndex.GetRootNames()));
    }
}

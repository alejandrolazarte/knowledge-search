using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Common;

namespace KnowledgeSearch.Core.UseCases.CodeGraph;

public sealed record ListRepositoriesCommand;

public sealed record ListRepositoriesResponse(IReadOnlyList<string> RepositoryNames);

public sealed class ListRepositoriesUseCase(ICodeGraphStore store)
    : IUseCase<ListRepositoriesCommand, ListRepositoriesResponse>
{
    public Task<Result<ListRepositoriesResponse>> ExecuteAsync(
        ListRepositoriesCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<Result<ListRepositoriesResponse>>(
            new ListRepositoriesResponse(store.GetRepositoryNames()));
    }
}

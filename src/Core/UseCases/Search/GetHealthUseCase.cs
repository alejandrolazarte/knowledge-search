using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Search;

namespace KnowledgeSearch.Core.UseCases.Search;

public sealed record GetHealthCommand;

public sealed record GetHealthResponse(HealthResult Health);

public sealed class GetHealthUseCase : IUseCase<GetHealthCommand, GetHealthResponse>
{
    public Task<Result<GetHealthResponse>> ExecuteAsync(
        GetHealthCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<Result<GetHealthResponse>>(new GetHealthResponse(new HealthResult("ok")));
    }
}

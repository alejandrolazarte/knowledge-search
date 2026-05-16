using KnowledgeSearch.Core.Common;

namespace KnowledgeSearch.Core.UseCases;

public interface IUseCase<in TCommand, TResponse>
{
    Task<Result<TResponse>> ExecuteAsync(TCommand command, CancellationToken cancellationToken);
}

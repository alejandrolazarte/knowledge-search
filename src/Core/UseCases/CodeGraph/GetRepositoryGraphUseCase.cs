using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.CodeGraph;

namespace KnowledgeSearch.Core.UseCases.CodeGraph;

public sealed record GetRepositoryGraphCommand(string RepositoryName);

public sealed class GetRepositoryGraphUseCase(ICodeGraphStore store)
    : IUseCase<GetRepositoryGraphCommand, CodeGraphResponse>
{
    public Task<Result<CodeGraphResponse>> ExecuteAsync(
        GetRepositoryGraphCommand command,
        CancellationToken cancellationToken)
    {
        if (!store.RepositoryExists(command.RepositoryName))
        {
            return Task.FromResult<Result<CodeGraphResponse>>(
                Result.NotFound<CodeGraphResponse>("Repositorio no encontrado"));
        }

        return Task.FromResult<Result<CodeGraphResponse>>(new CodeGraphResponse(
            store.GetNodes(command.RepositoryName).Select(CodeNodeResponse.From).ToList(),
            store.GetEdges(command.RepositoryName).Select(CodeEdgeResponse.From).ToList()));
    }
}

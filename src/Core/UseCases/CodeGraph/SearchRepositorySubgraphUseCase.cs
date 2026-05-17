using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.CodeGraph;

namespace KnowledgeSearch.Core.UseCases.CodeGraph;

public sealed record SearchRepositorySubgraphCommand(string RepositoryName, string? Query, int? Depth);

public sealed class SearchRepositorySubgraphUseCase(
    ICodeGraphStore store,
    ICodeGraphSearchService graphService)
    : IUseCase<SearchRepositorySubgraphCommand, CodeSubgraphResponse>
{
    public Task<Result<CodeSubgraphResponse>> ExecuteAsync(
        SearchRepositorySubgraphCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Query))
        {
            return Task.FromResult<Result<CodeSubgraphResponse>>(
                Result.Validation<CodeSubgraphResponse>("El parámetro q es requerido."));
        }

        if (!store.RepositoryExists(command.RepositoryName))
        {
            return Task.FromResult<Result<CodeSubgraphResponse>>(
                Result.NotFound<CodeSubgraphResponse>("Repositorio no encontrado"));
        }

        var actualDepth = Math.Clamp(command.Depth ?? 2, 0, 5);
        var subgraph = graphService.SearchSubgraph(command.RepositoryName, command.Query, actualDepth);

        return Task.FromResult<Result<CodeSubgraphResponse>>(new CodeSubgraphResponse(
            subgraph.Nodes
                .Select(node => CodeSearchNodeResponse.From(node, subgraph.NodeWeights.GetValueOrDefault(node.Identifier)))
                .ToList(),
            subgraph.Edges.Select(CodeEdgeResponse.From).ToList(),
            command.Query,
            actualDepth,
            subgraph.TotalFound));
    }
}

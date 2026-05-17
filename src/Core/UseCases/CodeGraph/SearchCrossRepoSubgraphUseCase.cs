using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.CodeGraph;

namespace KnowledgeSearch.Core.UseCases.CodeGraph;

public sealed record SearchCrossRepoSubgraphCommand(string? Query, int? Depth);

public sealed class SearchCrossRepoSubgraphUseCase(
    ICodeGraphStore store,
    ICodeGraphSearchService graphService)
    : IUseCase<SearchCrossRepoSubgraphCommand, CrossRepoSubgraphResponse>
{
    public Task<Result<CrossRepoSubgraphResponse>> ExecuteAsync(
        SearchCrossRepoSubgraphCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Query))
        {
            return Task.FromResult<Result<CrossRepoSubgraphResponse>>(
                Result.Validation<CrossRepoSubgraphResponse>("El parámetro q es requerido."));
        }

        var actualDepth = Math.Clamp(command.Depth ?? 2, 0, 5);
        var subgraph = graphService.SearchSubgraphAcrossRepositories(command.Query, actualDepth);
        var visitedIdentifiers = subgraph.Nodes
            .Select(node => (node.RepositoryName, node.Node.Identifier))
            .ToHashSet();

        var relevantCrossRepoLinks = store.GetCrossRepoEdges()
            .Where(edge => visitedIdentifiers.Contains((edge.SourceRepositoryName, edge.SourceIdentifier))
                        || visitedIdentifiers.Contains((edge.TargetRepositoryName, edge.TargetIdentifier)))
            .Select(CrossRepoLinkResponse.From)
            .ToList();

        var weightKey = (RepositoryBoundCodeNode node) => $"{node.RepositoryName}:{node.Node.Identifier}";

        return Task.FromResult<Result<CrossRepoSubgraphResponse>>(new CrossRepoSubgraphResponse(
            subgraph.Nodes
                .Select(node => CrossRepoSearchNodeResponse.From(node, subgraph.NodeWeights.GetValueOrDefault(weightKey(node))))
                .ToList(),
            subgraph.Edges.Select(CrossRepoSearchEdgeResponse.From).ToList(),
            relevantCrossRepoLinks,
            command.Query,
            actualDepth,
            subgraph.TotalFound));
    }
}

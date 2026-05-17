namespace KnowledgeSearch.Core.Domain.CodeGraph;

public sealed record RepositoryBoundCodeNode(string RepositoryName, CodeNode Node);

public sealed record RepositoryBoundCodeEdge(string RepositoryName, CodeEdge Edge);

public sealed record CrossRepoSubgraphResult(
    IReadOnlyList<RepositoryBoundCodeNode> Nodes,
    IReadOnlyList<RepositoryBoundCodeEdge> Edges,
    IReadOnlyDictionary<string, int> NodeWeights,
    int TotalFound);

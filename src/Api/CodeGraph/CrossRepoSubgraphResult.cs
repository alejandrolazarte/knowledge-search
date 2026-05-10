namespace KnowledgeSearch;

internal record RepositoryBoundCodeNode(string RepositoryName, CodeNode Node);

internal record RepositoryBoundCodeEdge(string RepositoryName, CodeEdge Edge);

internal record CrossRepoSubgraphResult(
    IReadOnlyList<RepositoryBoundCodeNode> Nodes,
    IReadOnlyList<RepositoryBoundCodeEdge> Edges,
    IReadOnlyDictionary<string, int> NodeWeights,
    int TotalFound);

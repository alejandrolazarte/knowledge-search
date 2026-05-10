namespace KnowledgeSearch;

internal record CodeSubgraphResult(
    IReadOnlyList<CodeNode> Nodes,
    IReadOnlyList<CodeEdge> Edges,
    IReadOnlyDictionary<string, int> NodeWeights,
    int TotalFound);

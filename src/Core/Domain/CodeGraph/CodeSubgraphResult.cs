namespace KnowledgeSearch.Core.Domain.CodeGraph;

public sealed record CodeSubgraphResult(
    IReadOnlyList<CodeNode> Nodes,
    IReadOnlyList<CodeEdge> Edges,
    IReadOnlyDictionary<string, int> NodeWeights,
    int TotalFound);

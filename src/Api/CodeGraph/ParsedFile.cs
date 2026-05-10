namespace KnowledgeSearch;

internal record ParsedFile(
    IReadOnlyList<CodeNode> Nodes,
    IReadOnlyList<CodeEdge> Edges);

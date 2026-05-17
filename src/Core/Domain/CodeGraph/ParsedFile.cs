namespace KnowledgeSearch.Core.Domain.CodeGraph;

public sealed record ParsedFile(
    IReadOnlyList<CodeNode> Nodes,
    IReadOnlyList<CodeEdge> Edges);

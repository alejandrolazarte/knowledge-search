namespace KnowledgeSearch.Core.Domain.CodeGraph;

public sealed record CodeGraphScanResult(
    IReadOnlyList<CodeNode> Nodes,
    IReadOnlyList<CodeEdge> Edges,
    int FilesScanned,
    int FilesSkipped);

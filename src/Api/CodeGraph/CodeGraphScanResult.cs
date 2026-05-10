namespace KnowledgeSearch;

internal record CodeGraphScanResult(
    IReadOnlyList<CodeNode> Nodes,
    IReadOnlyList<CodeEdge> Edges,
    int FilesScanned,
    int FilesSkipped);

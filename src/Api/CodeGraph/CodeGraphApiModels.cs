namespace KnowledgeSearch;

internal record CodeNodeApiResponse(string Identifier, string Name, string Kind, string FilePath, int Line)
{
    internal static CodeNodeApiResponse From(CodeNode node) =>
        new(node.Identifier, node.Name, node.Kind.ToString(), node.FilePath, node.Line);
}

internal record CodeEdgeApiResponse(string SourceIdentifier, string TargetIdentifier, string Kind, int Line)
{
    internal static CodeEdgeApiResponse From(CodeEdge edge) =>
        new(edge.SourceIdentifier, edge.TargetIdentifier, edge.Kind.ToString(), edge.Line);
}

internal record CodeGraphApiResponse(
    IReadOnlyList<CodeNodeApiResponse> Nodes,
    IReadOnlyList<CodeEdgeApiResponse> Edges);

internal record ScanSummaryApiResponse(
    string RepositoryName,
    int FilesScanned,
    int FilesSkipped,
    int NodesFound,
    int EdgesFound);

internal record ScanDirectoryRequest(string DirectoryPath);

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

internal record CodeSearchNodeApiResponse(string Identifier, string Name, string Kind, string FilePath, int Line, int Weight)
{
    internal static CodeSearchNodeApiResponse From(CodeNode node, int weight) =>
        new(node.Identifier, node.Name, node.Kind.ToString(), node.FilePath, node.Line, weight);
}

internal record CodeSubgraphApiResponse(
    IReadOnlyList<CodeSearchNodeApiResponse> Nodes,
    IReadOnlyList<CodeEdgeApiResponse> Edges,
    string Query,
    int Depth,
    int TotalFound);

internal record CrossRepoSearchNodeApiResponse(
    string RepositoryName,
    string Identifier,
    string Name,
    string Kind,
    string FilePath,
    int Line,
    int Weight)
{
    internal static CrossRepoSearchNodeApiResponse From(RepositoryBoundCodeNode boundNode, int weight) =>
        new(boundNode.RepositoryName,
            boundNode.Node.Identifier,
            boundNode.Node.Name,
            boundNode.Node.Kind.ToString(),
            boundNode.Node.FilePath,
            boundNode.Node.Line,
            weight);
}

internal record CrossRepoSearchEdgeApiResponse(
    string RepositoryName,
    string SourceIdentifier,
    string TargetIdentifier,
    string Kind,
    int Line)
{
    internal static CrossRepoSearchEdgeApiResponse From(RepositoryBoundCodeEdge boundEdge) =>
        new(boundEdge.RepositoryName,
            boundEdge.Edge.SourceIdentifier,
            boundEdge.Edge.TargetIdentifier,
            boundEdge.Edge.Kind.ToString(),
            boundEdge.Edge.Line);
}

internal record CrossRepoSubgraphApiResponse(
    IReadOnlyList<CrossRepoSearchNodeApiResponse> Nodes,
    IReadOnlyList<CrossRepoSearchEdgeApiResponse> Edges,
    IReadOnlyList<CrossRepoLinkApiResponse> CrossRepoLinks,
    string Query,
    int Depth,
    int TotalFound);

internal record CrossRepoLinkApiResponse(
    string SourceRepositoryName,
    string SourceIdentifier,
    string TargetRepositoryName,
    string TargetIdentifier,
    string Kind)
{
    internal static CrossRepoLinkApiResponse From(CrossRepoCodeEdge edge) =>
        new(edge.SourceRepositoryName, edge.SourceIdentifier, edge.TargetRepositoryName, edge.TargetIdentifier, edge.Kind.ToString());
}

internal record CrossRefSummaryApiResponse(int CrossRepoEdgesFound);

internal record CodeDocumentSearchApiResponse(
    string RepositoryName,
    string Identifier,
    string Name,
    string Kind,
    string FilePath,
    int Line,
    string Content,
    double Score)
{
    internal static CodeDocumentSearchApiResponse From(CodeDocumentSearchResult result) =>
        new(
            result.RepositoryName,
            result.Identifier,
            result.Name,
            result.Kind.ToString(),
            result.FilePath,
            result.Line,
            result.Content,
            result.Score);
}

using KnowledgeSearch.Core.Domain.CodeGraph;

namespace KnowledgeSearch.Core.UseCases.CodeGraph;

public sealed record CodeNodeResponse(string Identifier, string Name, string Kind, string FilePath, int Line)
{
    public static CodeNodeResponse From(CodeNode node) =>
        new(node.Identifier, node.Name, node.Kind.ToString(), node.FilePath, node.Line);
}

public sealed record CodeEdgeResponse(string SourceIdentifier, string TargetIdentifier, string Kind, int Line)
{
    public static CodeEdgeResponse From(CodeEdge edge) =>
        new(edge.SourceIdentifier, edge.TargetIdentifier, edge.Kind.ToString(), edge.Line);
}

public sealed record CodeGraphResponse(
    IReadOnlyList<CodeNodeResponse> Nodes,
    IReadOnlyList<CodeEdgeResponse> Edges);

public sealed record ScanRepositoryResponse(
    string RepositoryName,
    int FilesScanned,
    int FilesSkipped,
    int NodesFound,
    int EdgesFound);

public sealed record CodeSearchNodeResponse(string Identifier, string Name, string Kind, string FilePath, int Line, int Weight)
{
    public static CodeSearchNodeResponse From(CodeNode node, int weight) =>
        new(node.Identifier, node.Name, node.Kind.ToString(), node.FilePath, node.Line, weight);
}

public sealed record CodeSubgraphResponse(
    IReadOnlyList<CodeSearchNodeResponse> Nodes,
    IReadOnlyList<CodeEdgeResponse> Edges,
    string Query,
    int Depth,
    int TotalFound);

public sealed record CrossRepoSearchNodeResponse(
    string RepositoryName,
    string Identifier,
    string Name,
    string Kind,
    string FilePath,
    int Line,
    int Weight)
{
    public static CrossRepoSearchNodeResponse From(RepositoryBoundCodeNode boundNode, int weight) =>
        new(boundNode.RepositoryName,
            boundNode.Node.Identifier,
            boundNode.Node.Name,
            boundNode.Node.Kind.ToString(),
            boundNode.Node.FilePath,
            boundNode.Node.Line,
            weight);
}

public sealed record CrossRepoSearchEdgeResponse(
    string RepositoryName,
    string SourceIdentifier,
    string TargetIdentifier,
    string Kind,
    int Line)
{
    public static CrossRepoSearchEdgeResponse From(RepositoryBoundCodeEdge boundEdge) =>
        new(boundEdge.RepositoryName,
            boundEdge.Edge.SourceIdentifier,
            boundEdge.Edge.TargetIdentifier,
            boundEdge.Edge.Kind.ToString(),
            boundEdge.Edge.Line);
}

public sealed record CrossRepoLinkResponse(
    string SourceRepositoryName,
    string SourceIdentifier,
    string TargetRepositoryName,
    string TargetIdentifier,
    string Kind)
{
    public static CrossRepoLinkResponse From(CrossRepoCodeEdge edge) =>
        new(edge.SourceRepositoryName, edge.SourceIdentifier, edge.TargetRepositoryName, edge.TargetIdentifier, edge.Kind.ToString());
}

public sealed record CrossRepoSubgraphResponse(
    IReadOnlyList<CrossRepoSearchNodeResponse> Nodes,
    IReadOnlyList<CrossRepoSearchEdgeResponse> Edges,
    IReadOnlyList<CrossRepoLinkResponse> CrossRepoLinks,
    string Query,
    int Depth,
    int TotalFound);

public sealed record CrossRefSummaryResponse(int CrossRepoEdgesFound);

public sealed record CodeDocumentSearchResponse(
    string RepositoryName,
    string Identifier,
    string Name,
    string Kind,
    string FilePath,
    int Line,
    string Content,
    double Score)
{
    public static CodeDocumentSearchResponse From(CodeDocumentSearchResult result) =>
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

namespace KnowledgeSearch.Core.Domain.CodeGraph;

public sealed record CodeEdge(
    string SourceIdentifier,
    string TargetIdentifier,
    CodeEdgeKind Kind,
    int Line);

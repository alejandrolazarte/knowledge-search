namespace KnowledgeSearch;

internal record CodeEdge(
    string SourceIdentifier,
    string TargetIdentifier,
    CodeEdgeKind Kind,
    int Line);

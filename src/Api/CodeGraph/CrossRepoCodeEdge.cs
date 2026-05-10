namespace KnowledgeSearch;

internal enum CrossRepoEdgeKind { References }

internal record CrossRepoCodeEdge(
    string SourceRepositoryName,
    string SourceIdentifier,
    string TargetRepositoryName,
    string TargetIdentifier,
    CrossRepoEdgeKind Kind);

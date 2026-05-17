namespace KnowledgeSearch.Core.Domain.CodeGraph;

public enum CrossRepoEdgeKind
{
    References,
}

public sealed record CrossRepoCodeEdge(
    string SourceRepositoryName,
    string SourceIdentifier,
    string TargetRepositoryName,
    string TargetIdentifier,
    CrossRepoEdgeKind Kind);

namespace KnowledgeSearch.Core.Domain.CodeGraph;

public sealed record CodeNode(
    string Identifier,
    string Name,
    CodeNodeKind Kind,
    string FilePath,
    int Line);

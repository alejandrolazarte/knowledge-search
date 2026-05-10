namespace KnowledgeSearch;

internal record CodeNode(
    string Identifier,
    string Name,
    CodeNodeKind Kind,
    string FilePath,
    int Line);

namespace KnowledgeSearch;

[Flags]
internal enum SearchMode
{
    None = 0,
    Phrase = 1,
    And = 2,
    Or = 4,
    Default = Phrase | And | Or,
}

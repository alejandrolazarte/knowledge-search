namespace KnowledgeSearch.Core.Domain.Search;

[Flags]
public enum SearchMode
{
    None = 0,
    Phrase = 1,
    And = 2,
    Or = 4,
    Default = Phrase | And | Or,
}

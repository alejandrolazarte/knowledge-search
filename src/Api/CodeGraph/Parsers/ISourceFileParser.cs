namespace KnowledgeSearch;

internal interface ISourceFileParser
{
    bool CanParse(string filePath);
    ParsedFile Parse(string filePath);
}

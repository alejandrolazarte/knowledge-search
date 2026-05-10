namespace KnowledgeSearch;

internal interface ISourceFileParser
{
    ParsedFile Parse(string filePath);
}

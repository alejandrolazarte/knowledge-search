namespace KnowledgeSearch;

internal interface ICodeGraphService
{
    CodeGraphScanResult ScanDirectory(string directoryPath);
}

using KnowledgeSearch.Core.Abstractions.CodeGraph;

namespace KnowledgeSearch;

internal sealed class CodeGraphScannerAdapter(ICodeGraphService codeGraphService) : ICodeGraphScanner
{
    public void ScanDirectory(string directoryPath)
    {
        codeGraphService.ScanDirectory(directoryPath);
    }
}

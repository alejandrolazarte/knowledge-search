using Microsoft.CodeAnalysis.CSharp;

namespace KnowledgeSearch;

internal sealed class CSharpParser : ISourceFileParser
{
    public ParsedFile Parse(string filePath)
    {
        var sourceCode = File.ReadAllText(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilationRoot = syntaxTree.GetCompilationUnitRoot();

        var walker = new CodeStructureWalker(filePath);
        walker.Visit(compilationRoot);

        return new ParsedFile(walker.ExtractedNodes, walker.ExtractedEdges);
    }
}

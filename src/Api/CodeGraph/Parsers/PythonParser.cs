using System.Text.RegularExpressions;

namespace KnowledgeSearch;

internal sealed class PythonParser : ISourceFileParser
{
    private static readonly Regex ClassDeclarationPattern = new(
        @"^class\s+(\w+)\s*(?:\(([^)]*)\))?:",
        RegexOptions.Compiled);

    private static readonly Regex FunctionDeclarationPattern = new(
        @"^(\s*)def\s+(\w+)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex ImportPattern = new(
        @"^import\s+(\S+)",
        RegexOptions.Compiled);

    private static readonly Regex FromImportPattern = new(
        @"^from\s+(\S+)\s+import",
        RegexOptions.Compiled);

    public ParsedFile Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var nodes = new List<CodeNode>();
        var edges = new List<CodeEdge>();

        string? currentClassIdentifier = null;
        var classIndentationLength = 0;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var lineNumber = lineIndex + 1;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var importMatch = ImportPattern.Match(line);
            if (importMatch.Success)
            {
                var moduleName = importMatch.Groups[1].Value.Split('.')[0];
                edges.Add(new CodeEdge(Path.GetFileNameWithoutExtension(filePath), moduleName, CodeEdgeKind.Imports, lineNumber));
                continue;
            }

            var fromImportMatch = FromImportPattern.Match(line);
            if (fromImportMatch.Success)
            {
                var moduleName = fromImportMatch.Groups[1].Value.Split('.')[0];
                edges.Add(new CodeEdge(Path.GetFileNameWithoutExtension(filePath), moduleName, CodeEdgeKind.Imports, lineNumber));
                continue;
            }

            var classMatch = ClassDeclarationPattern.Match(line);
            if (classMatch.Success)
            {
                var className = classMatch.Groups[1].Value;
                nodes.Add(new CodeNode(className, className, CodeNodeKind.Class, filePath, lineNumber));

                var basesGroup = classMatch.Groups[2].Value;
                foreach (var baseName in ExtractBaseClassNames(basesGroup))
                {
                    edges.Add(new CodeEdge(className, baseName, CodeEdgeKind.Inherits, lineNumber));
                }

                currentClassIdentifier = className;
                classIndentationLength = CountLeadingSpaces(line);
                continue;
            }

            var functionMatch = FunctionDeclarationPattern.Match(line);
            if (functionMatch.Success)
            {
                var functionIndentationLength = functionMatch.Groups[1].Value.Length;
                var functionName = functionMatch.Groups[2].Value;

                if (currentClassIdentifier is not null && functionIndentationLength > classIndentationLength)
                {
                    var methodIdentifier = $"{currentClassIdentifier}.{functionName}";
                    nodes.Add(new CodeNode(methodIdentifier, functionName, CodeNodeKind.Method, filePath, lineNumber));
                    edges.Add(new CodeEdge(currentClassIdentifier, methodIdentifier, CodeEdgeKind.Contains, lineNumber));
                }
                else
                {
                    if (functionIndentationLength == 0)
                    {
                        currentClassIdentifier = null;
                    }

                    nodes.Add(new CodeNode(functionName, functionName, CodeNodeKind.Method, filePath, lineNumber));
                }

                continue;
            }

            if (currentClassIdentifier is not null && CountLeadingSpaces(line) <= classIndentationLength && line[0] != ' ' && line[0] != '\t')
            {
                currentClassIdentifier = null;
            }
        }

        return new ParsedFile(nodes, edges);
    }

    private static IEnumerable<string> ExtractBaseClassNames(string basesGroup) =>
        basesGroup
            .Split(',')
            .Select(name => name.Trim())
            .Where(name => name.Length > 0 && name != "object");

    private static int CountLeadingSpaces(string line)
    {
        var count = 0;
        foreach (var character in line)
        {
            if (character == ' ') { count++; }
            else if (character == '\t') { count += 4; }
            else { break; }
        }

        return count;
    }
}

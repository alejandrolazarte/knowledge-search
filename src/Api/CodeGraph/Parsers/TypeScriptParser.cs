using System.Text.RegularExpressions;

namespace KnowledgeSearch;

internal sealed class TypeScriptParser : ISourceFileParser
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ts", ".tsx", ".js", ".jsx", ".mjs",
    };

    private static readonly HashSet<string> ControlFlowKeywords = new(StringComparer.Ordinal)
    {
        "if", "for", "while", "switch", "catch", "else", "do", "try",
        "return", "typeof", "instanceof", "delete", "void", "throw", "await",
        "import", "export", "const", "let", "var", "new", "super",
    };

    private static readonly Regex ClassDeclarationPattern = new(
        @"^\s*(?:export\s+)?(?:default\s+)?(?:abstract\s+)?class\s+(\w+)",
        RegexOptions.Compiled);

    private static readonly Regex ExtendsPattern = new(
        @"\bextends\s+(\w+)",
        RegexOptions.Compiled);

    private static readonly Regex ImplementsPattern = new(
        @"\bimplements\s+([\w,\s<>[\]]+?)(?=\s*\{|$)",
        RegexOptions.Compiled);

    private static readonly Regex InterfaceDeclarationPattern = new(
        @"^\s*(?:export\s+)?interface\s+(\w+)",
        RegexOptions.Compiled);

    private static readonly Regex TopLevelFunctionDeclarationPattern = new(
        @"^(?:export\s+)?(?:default\s+)?(?:async\s+)?function\s+(\w+)\s*[(<]",
        RegexOptions.Compiled);

    private static readonly Regex MethodDeclarationPattern = new(
        @"^\s{2,}(?:(?:public|private|protected|static|async|override|abstract|readonly)\s+)*([a-zA-Z_$][\w$]*)\s*[(<]",
        RegexOptions.Compiled);

    private static readonly Regex ImportFromPattern = new(
        @"^import\s+.+\bfrom\s+['""]([^'""]+)['""]",
        RegexOptions.Compiled);

    public bool CanParse(string filePath) =>
        SupportedExtensions.Contains(Path.GetExtension(filePath));

    public ParsedFile Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var nodes = new List<CodeNode>();
        var edges = new List<CodeEdge>();

        var braceDepth = 0;
        var classOpenDepth = -1;
        string? currentClassIdentifier = null;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var lineNumber = lineIndex + 1;

            var importMatch = ImportFromPattern.Match(line);
            if (importMatch.Success)
            {
                var moduleIdentifier = importMatch.Groups[1].Value;
                edges.Add(new CodeEdge(Path.GetFileNameWithoutExtension(filePath), moduleIdentifier, CodeEdgeKind.Imports, lineNumber));
            }

            var interfaceMatch = InterfaceDeclarationPattern.Match(line);
            if (interfaceMatch.Success)
            {
                var interfaceName = interfaceMatch.Groups[1].Value;
                nodes.Add(new CodeNode(interfaceName, interfaceName, CodeNodeKind.Interface, filePath, lineNumber));
            }

            var classMatch = ClassDeclarationPattern.Match(line);
            if (classMatch.Success)
            {
                var className = classMatch.Groups[1].Value;
                nodes.Add(new CodeNode(className, className, CodeNodeKind.Class, filePath, lineNumber));

                var extendsMatch = ExtendsPattern.Match(line);
                if (extendsMatch.Success)
                {
                    edges.Add(new CodeEdge(className, extendsMatch.Groups[1].Value, CodeEdgeKind.Inherits, lineNumber));
                }

                var implementsMatch = ImplementsPattern.Match(line);
                if (implementsMatch.Success)
                {
                    foreach (var implementedInterface in SplitImplementsList(implementsMatch.Groups[1].Value))
                    {
                        edges.Add(new CodeEdge(className, implementedInterface, CodeEdgeKind.Implements, lineNumber));
                    }
                }

                classOpenDepth = braceDepth;
                currentClassIdentifier = className;
            }

            var functionMatch = TopLevelFunctionDeclarationPattern.Match(line);
            if (functionMatch.Success && currentClassIdentifier is null)
            {
                var functionName = functionMatch.Groups[1].Value;
                nodes.Add(new CodeNode(functionName, functionName, CodeNodeKind.Method, filePath, lineNumber));
            }

            if (currentClassIdentifier is not null && braceDepth == classOpenDepth + 1)
            {
                var methodMatch = MethodDeclarationPattern.Match(line);
                if (methodMatch.Success)
                {
                    var methodName = methodMatch.Groups[1].Value;
                    if (!ControlFlowKeywords.Contains(methodName))
                    {
                        var methodIdentifier = $"{currentClassIdentifier}.{methodName}";
                        nodes.Add(new CodeNode(methodIdentifier, methodName, CodeNodeKind.Method, filePath, lineNumber));
                        edges.Add(new CodeEdge(currentClassIdentifier, methodIdentifier, CodeEdgeKind.Contains, lineNumber));
                    }
                }
            }

            braceDepth += CountBraceDelta(line);

            if (currentClassIdentifier is not null && braceDepth <= classOpenDepth)
            {
                currentClassIdentifier = null;
                classOpenDepth = -1;
            }
        }

        return new ParsedFile(nodes, edges);
    }

    private static IEnumerable<string> SplitImplementsList(string implementsList) =>
        implementsList
            .Split(',')
            .Select(name => name.Trim().Split('<')[0].Trim())
            .Where(name => name.Length > 0);

    private static int CountBraceDelta(string line)
    {
        var delta = 0;
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inTemplateLiteral = false;

        for (var charIndex = 0; charIndex < line.Length; charIndex++)
        {
            var character = line[charIndex];

            if (character == '\\' && (inSingleQuote || inDoubleQuote || inTemplateLiteral))
            {
                charIndex++;
                continue;
            }

            if (character == '\'' && !inDoubleQuote && !inTemplateLiteral) { inSingleQuote = !inSingleQuote; continue; }
            if (character == '"' && !inSingleQuote && !inTemplateLiteral) { inDoubleQuote = !inDoubleQuote; continue; }
            if (character == '`' && !inSingleQuote && !inDoubleQuote) { inTemplateLiteral = !inTemplateLiteral; continue; }
            if (character == '/' && charIndex + 1 < line.Length && line[charIndex + 1] == '/') { break; }

            if (inSingleQuote || inDoubleQuote || inTemplateLiteral) { continue; }

            if (character == '{') { delta++; }
            else if (character == '}') { delta--; }
        }

        return delta;
    }
}

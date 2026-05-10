using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeSearch;

internal sealed class CodeGraphService(IServiceProvider serviceProvider) : ICodeGraphService
{
    private readonly IKeyedServiceProvider _keyedServiceProvider = (IKeyedServiceProvider)serviceProvider;

    private static readonly IReadOnlySet<string> ExcludedDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", "bin", "obj",
    };

    public CodeGraphScanResult ScanDirectory(string directoryPath)
    {
        var allNodes = new List<CodeNode>();
        var allEdges = new List<CodeEdge>();
        var filesScanned = 0;
        var filesSkipped = 0;

        foreach (var filePath in EnumerateSourceFiles(directoryPath))
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var parser = _keyedServiceProvider.GetKeyedService<ISourceFileParser>(extension);

            if (parser is null)
            {
                filesSkipped++;
                continue;
            }

            var parsedFile = parser.Parse(filePath);
            allNodes.AddRange(parsedFile.Nodes);
            allEdges.AddRange(parsedFile.Edges);
            filesScanned++;
        }

        return new CodeGraphScanResult(allNodes, allEdges, filesScanned, filesSkipped);
    }

    private static IEnumerable<string> EnumerateSourceFiles(string directoryPath) =>
        Directory
            .EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Where(filePath => !ContainsExcludedDirectory(filePath));

    private static bool ContainsExcludedDirectory(string filePath)
    {
        var separator = Path.DirectorySeparatorChar;
        return ExcludedDirectoryNames.Any(excluded =>
            filePath.Contains($"{separator}{excluded}{separator}", StringComparison.OrdinalIgnoreCase));
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeSearch;

internal sealed class CodeGraphService(
    IServiceProvider serviceProvider,
    ICodeGraphRepository repository) : ICodeGraphService
{
    private readonly IKeyedServiceProvider _keyedServiceProvider = (IKeyedServiceProvider)serviceProvider;

    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
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

        var scanResult = new CodeGraphScanResult(allNodes, allEdges, filesScanned, filesSkipped);
        var repositoryName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        repository.SaveScanResult(repositoryName, scanResult);

        return scanResult;
    }

    public CodeSubgraphResult SearchSubgraph(string repositoryName, string query, int depth)
    {
        var seedNodes = repository.SearchNodes(repositoryName, query);

        if (seedNodes.Count == 0)
        {
            return new CodeSubgraphResult([], [], new Dictionary<string, int>(), 0);
        }

        var allNodes = repository.GetNodes(repositoryName).ToDictionary(n => n.Identifier, StringComparer.Ordinal);
        var allEdges = repository.GetEdges(repositoryName);

        var adjacency = BuildBidirectionalAdjacency(allEdges);

        var visitedIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<(string Identifier, int CurrentDepth)>();

        foreach (var seed in seedNodes)
        {
            visitedIdentifiers.Add(seed.Identifier);
            queue.Enqueue((seed.Identifier, 0));
        }

        while (queue.Count > 0)
        {
            var (identifier, currentDepth) = queue.Dequeue();

            if (currentDepth >= depth || !adjacency.TryGetValue(identifier, out var neighbors))
            {
                continue;
            }

            foreach (var neighbor in neighbors)
            {
                if (!visitedIdentifiers.Contains(neighbor) && allNodes.ContainsKey(neighbor))
                {
                    visitedIdentifiers.Add(neighbor);
                    queue.Enqueue((neighbor, currentDepth + 1));
                }
            }
        }

        var resultEdges = allEdges
            .Where(e => visitedIdentifiers.Contains(e.SourceIdentifier) && visitedIdentifiers.Contains(e.TargetIdentifier))
            .ToList();

        var nodeWeights = ComputeNodeWeights(resultEdges);

        var resultNodes = visitedIdentifiers
            .Where(allNodes.ContainsKey)
            .Select(id => allNodes[id])
            .ToList();

        return new CodeSubgraphResult(resultNodes, resultEdges, nodeWeights, seedNodes.Count);
    }

    public CrossRepoSubgraphResult SearchSubgraphAcrossRepositories(string query, int depth)
    {
        var allRepositoryNames = repository.GetRepositoryNames();
        var resultNodes = new List<RepositoryBoundCodeNode>();
        var resultEdges = new List<RepositoryBoundCodeEdge>();
        var totalFound = 0;

        foreach (var repositoryName in allRepositoryNames)
        {
            var subgraph = SearchSubgraph(repositoryName, query, depth);
            if (subgraph.TotalFound == 0)
            {
                continue;
            }

            totalFound += subgraph.TotalFound;
            resultNodes.AddRange(subgraph.Nodes.Select(n => new RepositoryBoundCodeNode(repositoryName, n)));
            resultEdges.AddRange(subgraph.Edges.Select(e => new RepositoryBoundCodeEdge(repositoryName, e)));
        }

        var nodeWeights = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var boundEdge in resultEdges)
        {
            var sourceKey = $"{boundEdge.RepositoryName}:{boundEdge.Edge.SourceIdentifier}";
            var targetKey = $"{boundEdge.RepositoryName}:{boundEdge.Edge.TargetIdentifier}";
            nodeWeights.TryGetValue(sourceKey, out var sourceWeight);
            nodeWeights[sourceKey] = sourceWeight + 1;
            nodeWeights.TryGetValue(targetKey, out var targetWeight);
            nodeWeights[targetKey] = targetWeight + 1;
        }

        return new CrossRepoSubgraphResult(resultNodes, resultEdges, nodeWeights, totalFound);
    }

    private static Dictionary<string, HashSet<string>> BuildBidirectionalAdjacency(IReadOnlyList<CodeEdge> edges)
    {
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (!adjacency.TryGetValue(edge.SourceIdentifier, out var sourceNeighbors))
            {
                adjacency[edge.SourceIdentifier] = sourceNeighbors = new HashSet<string>(StringComparer.Ordinal);
            }
            sourceNeighbors.Add(edge.TargetIdentifier);

            if (!adjacency.TryGetValue(edge.TargetIdentifier, out var targetNeighbors))
            {
                adjacency[edge.TargetIdentifier] = targetNeighbors = new HashSet<string>(StringComparer.Ordinal);
            }
            targetNeighbors.Add(edge.SourceIdentifier);
        }

        return adjacency;
    }

    private static Dictionary<string, int> ComputeNodeWeights(IReadOnlyList<CodeEdge> edges)
    {
        var weights = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            weights.TryGetValue(edge.SourceIdentifier, out var srcWeight);
            weights[edge.SourceIdentifier] = srcWeight + 1;

            weights.TryGetValue(edge.TargetIdentifier, out var tgtWeight);
            weights[edge.TargetIdentifier] = tgtWeight + 1;
        }

        return weights;
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

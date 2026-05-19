using System.Collections.Concurrent;
using KnowledgeSearch.Core.Domain.Sources;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeSearch;

internal sealed class CodeGraphService(
    IServiceProvider serviceProvider,
    ICodeGraphRepository repository) : ICodeGraphService
{
    private readonly IKeyedServiceProvider _keyedServiceProvider = (IKeyedServiceProvider)serviceProvider;
    private readonly RecursiveDirectoryWalker _walker = new();

    private static readonly IReadOnlyList<string> DefaultExcludedDirectoryNames =
    [
        "node_modules", ".git", "bin", "obj",
        "dist", "build", "out", "coverage", "TestResults",
        ".next", ".nuxt", ".turbo", ".cache", ".vite", ".svelte-kit",
        ".pnpm-store", "storybook-static", ".vs", ".idea",
    ];

    public CodeGraphScanResult ScanDirectory(string directoryPath)
    {
        return ScanDirectory(BuildDefaultSource(directoryPath));
    }

    public CodeGraphScanResult ScanDirectory(ConfiguredSource source)
    {
        var directoryPath = source.GetAccessiblePath();
        var allNodes = new ConcurrentBag<CodeNode>();
        var allEdges = new ConcurrentBag<CodeEdge>();
        var filesScanned = 0;
        var filesSkipped = 0;

        var sourceFiles = EnumerateSourceFiles(directoryPath, source.Excludes);

        Parallel.ForEach(sourceFiles, filePath =>
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var parser = _keyedServiceProvider.GetKeyedService<ISourceFileParser>(extension);

            if (parser is null)
            {
                Interlocked.Increment(ref filesSkipped);
                return;
            }

            var parsedFile = parser.Parse(filePath);
            foreach (var node in parsedFile.Nodes)
            {
                allNodes.Add(node);
            }
            foreach (var edge in parsedFile.Edges)
            {
                allEdges.Add(edge);
            }
            Interlocked.Increment(ref filesScanned);
        });

        var scanResult = new CodeGraphScanResult(allNodes.ToList(), allEdges.ToList(), filesScanned, filesSkipped);
        var repositoryName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        repository.SaveScanResult(repositoryName, scanResult);

        return scanResult;
    }

    private static ConfiguredSource BuildDefaultSource(string directoryPath) =>
        ConfiguredSource.Create(
            id:       ConfiguredSource.CreateId(directoryPath),
            name:     ConfiguredSource.CreateId(directoryPath),
            kind:     SourceKind.Repository,
            hostPath: directoryPath);

    public CodeSubgraphResult SearchSubgraph(string repositoryName, string query, int depth)
    {
        var seedNodes = repository.SearchNodes(repositoryName, query);

        if (seedNodes.Count == 0)
        {
            return new CodeSubgraphResult([], [], new Dictionary<string, int>(), 0);
        }

        var allNodes = repository.GetNodes(repositoryName)
            .GroupBy(n => n.Identifier, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
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

    private const int CrossRepoMinimumNodeNameLength = 8;

    public IReadOnlyList<CrossRepoCodeEdge> BuildCrossRepoEdges()
    {
        var allRepositoryNames = repository.GetRepositoryNames();

        if (allRepositoryNames.Count < 2)
        {
            return [];
        }

        var nodesByRepo = allRepositoryNames.ToDictionary(
            repoName => repoName,
            repoName => repository.GetNodes(repoName),
            StringComparer.Ordinal);

        var edgesByRepo = allRepositoryNames.ToDictionary(
            repoName => repoName,
            repoName => repository.GetEdges(repoName),
            StringComparer.Ordinal);

        var crossRepoEdges = new HashSet<(string SourceRepo, string SourceId, string TargetRepo, string TargetId)>();

        foreach (var (candidateRepo, candidateNodes) in nodesByRepo)
        {
            var qualifiedCandidateNodes = candidateNodes
                .Where(n => n.Name.Length >= CrossRepoMinimumNodeNameLength)
                .ToList();

            if (qualifiedCandidateNodes.Count == 0)
            {
                continue;
            }

            foreach (var (searchRepo, searchNodes) in nodesByRepo)
            {
                if (searchRepo == candidateRepo)
                {
                    continue;
                }

                var searchRepoEdges = edgesByRepo[searchRepo];

                foreach (var candidateNode in qualifiedCandidateNodes)
                {
                    foreach (var searchNode in searchNodes)
                    {
                        if (searchNode.Name.Contains(candidateNode.Name, StringComparison.Ordinal))
                        {
                            crossRepoEdges.Add((searchRepo, searchNode.Identifier, candidateRepo, candidateNode.Identifier));
                        }
                    }

                    foreach (var edge in searchRepoEdges)
                    {
                        if (edge.TargetIdentifier.Contains(candidateNode.Name, StringComparison.Ordinal))
                        {
                            crossRepoEdges.Add((searchRepo, edge.SourceIdentifier, candidateRepo, candidateNode.Identifier));
                        }
                    }
                }
            }
        }

        var result = crossRepoEdges
            .Select(e => new CrossRepoCodeEdge(e.SourceRepo, e.SourceId, e.TargetRepo, e.TargetId, CrossRepoEdgeKind.References))
            .ToList();

        repository.SaveCrossRepoEdges(result);

        return result;
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

    /// <summary>
    /// Devuelve todos los ficheros del repo no excluidos por nombre de carpeta
    /// ni por glob de la fuente. La extension <em>no</em> se filtra aqui:
    /// CodeGraphService cuenta como <c>FilesSkipped</c> los ficheros que llegan
    /// sin parser registrado.
    /// </summary>
    private IReadOnlyList<string> EnumerateSourceFiles(string directoryPath, IReadOnlyList<string> additionalExcludeGlobs)
    {
        return _walker.Enumerate(directoryPath, new DirectoryWalkOptions(
            IncludeExtensions:      [],
            ExcludedDirectoryNames: DefaultExcludedDirectoryNames,
            ExcludeGlobs:           additionalExcludeGlobs ?? [])).Files;
    }
}

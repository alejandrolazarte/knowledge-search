namespace KnowledgeSearch;

internal static class CodeGraphEndpoints
{
    internal static void MapCodeGraphRoutes(this WebApplication app)
    {
        app.MapGet("/repos", (ICodeGraphRepository repository) =>
            Results.Ok(repository.GetRepositoryNames()));

        app.MapGet("/repos/{name}/graph", (string name, ICodeGraphRepository repository) =>
        {
            if (!repository.RepositoryExists(name))
            {
                return Results.NotFound();
            }

            var response = new CodeGraphApiResponse(
                repository.GetNodes(name).Select(CodeNodeApiResponse.From).ToList(),
                repository.GetEdges(name).Select(CodeEdgeApiResponse.From).ToList());

            return Results.Ok(response);
        });

        app.MapPost("/repos/scan", (ScanDirectoryRequest request, ICodeGraphService graphService) =>
        {
            if (string.IsNullOrWhiteSpace(request.DirectoryPath))
            {
                return Results.BadRequest(new ErrorResult("El campo directoryPath es requerido."));
            }

            if (!Directory.Exists(request.DirectoryPath))
            {
                return Results.BadRequest(new ErrorResult($"El directorio no existe: {request.DirectoryPath}"));
            }

            var scanResult = graphService.ScanDirectory(request.DirectoryPath);
            var repositoryName = Path.GetFileName(
                request.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            return Results.Ok(new ScanSummaryApiResponse(
                repositoryName,
                scanResult.FilesScanned,
                scanResult.FilesSkipped,
                scanResult.Nodes.Count,
                scanResult.Edges.Count));
        });

        app.MapPost("/repos/cross-ref", (ICodeGraphService graphService) =>
        {
            var crossRepoEdges = graphService.BuildCrossRepoEdges();
            return Results.Ok(new CrossRefSummaryApiResponse(crossRepoEdges.Count));
        });

        app.MapGet("/repos/code-search", (
            string? q,
            int? limit,
            string? modes,
            string? repos,
            string? kinds,
            ICodeGraphRepository repository) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new ErrorResult("El parámetro q es requerido."));
            }

            SearchMode searchModes;
            try
            {
                searchModes = modes is not null
                    ? Enum.Parse<SearchMode>(modes, ignoreCase: true)
                    : SearchMode.Default;
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new ErrorResult($"modes inválido: '{modes}'. Valores válidos: phrase, and, or"));
            }

            IReadOnlyList<CodeNodeKind>? kindFilters;
            try
            {
                kindFilters = kinds?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(kind => Enum.Parse<CodeNodeKind>(kind, ignoreCase: true))
                    .ToList();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new ErrorResult($"kinds inválido: '{kinds}'. Valores válidos: Class, Interface, Record, Enum, Method"));
            }

            var repoFilters = repos?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            try
            {
                var results = repository.SearchCodeDocuments(
                    q,
                    Math.Clamp(limit ?? 10, 1, 100),
                    searchModes,
                    repoFilters,
                    kindFilters);

                return Results.Ok(results.Select(CodeDocumentSearchApiResponse.From).ToList());
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.BadRequest(new ErrorResult($"Query inválida: {ex.Message}"));
            }
        });

        app.MapGet("/repos/file", (string path, ICodeGraphRepository repository) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.BadRequest(new ErrorResult("Falta parámetro path"));
            }

            var fullPath = Path.GetFullPath(path);
            if (!repository.IsCodePathAllowed(fullPath))
            {
                return Results.BadRequest(new ErrorResult("Ruta fuera de los repos escaneados"));
            }

            if (!File.Exists(fullPath))
            {
                return Results.NotFound();
            }

            return Results.Text(File.ReadAllText(fullPath), "text/plain; charset=utf-8");
        });

        app.MapGet("/repos/search", (string? q, int? depth, ICodeGraphRepository repository, ICodeGraphService graphService) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new ErrorResult("El parámetro q es requerido."));
            }

            var actualDepth = Math.Clamp(depth ?? 2, 0, 5);
            var subgraph = graphService.SearchSubgraphAcrossRepositories(q, actualDepth);
            var weightKey = (RepositoryBoundCodeNode n) => $"{n.RepositoryName}:{n.Node.Identifier}";

            var allCrossRepoEdges = repository.GetCrossRepoEdges();
            var visitedIdentifiers = subgraph.Nodes
                .Select(n => (n.RepositoryName, n.Node.Identifier))
                .ToHashSet();

            var relevantCrossRepoLinks = allCrossRepoEdges
                .Where(e => visitedIdentifiers.Contains((e.SourceRepositoryName, e.SourceIdentifier))
                         || visitedIdentifiers.Contains((e.TargetRepositoryName, e.TargetIdentifier)))
                .Select(CrossRepoLinkApiResponse.From)
                .ToList();

            var response = new CrossRepoSubgraphApiResponse(
                subgraph.Nodes
                    .Select(n => CrossRepoSearchNodeApiResponse.From(n, subgraph.NodeWeights.GetValueOrDefault(weightKey(n))))
                    .ToList(),
                subgraph.Edges.Select(CrossRepoSearchEdgeApiResponse.From).ToList(),
                relevantCrossRepoLinks,
                q,
                actualDepth,
                subgraph.TotalFound);

            return Results.Ok(response);
        });

        app.MapGet("/repos/{name}/search", (string name, string? q, int? depth, ICodeGraphRepository repository, ICodeGraphService graphService) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new ErrorResult("El parámetro q es requerido."));
            }

            if (!repository.RepositoryExists(name))
            {
                return Results.NotFound();
            }

            var actualDepth = Math.Clamp(depth ?? 2, 0, 5);
            var subgraph = graphService.SearchSubgraph(name, q, actualDepth);

            var response = new CodeSubgraphApiResponse(
                subgraph.Nodes
                    .Select(n => CodeSearchNodeApiResponse.From(n, subgraph.NodeWeights.GetValueOrDefault(n.Identifier)))
                    .ToList(),
                subgraph.Edges.Select(CodeEdgeApiResponse.From).ToList(),
                q,
                actualDepth,
                subgraph.TotalFound);

            return Results.Ok(response);
        });
    }
}

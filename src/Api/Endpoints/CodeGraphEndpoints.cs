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

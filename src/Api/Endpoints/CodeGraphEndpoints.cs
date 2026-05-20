using KnowledgeSearch.Core.Abstractions.Jobs;
using KnowledgeSearch.Core.Domain.Search;

using KnowledgeSearch.Core.UseCases.CodeGraph;
using KnowledgeSearch.Core.UseCases.Sources;

namespace KnowledgeSearch;

internal static class CodeGraphEndpoints
{
    internal static void MapCodeGraphRoutes(this WebApplication app)
    {
        app.MapGet("/repos", async (
            ListRepositoriesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new ListRepositoriesCommand(), cancellationToken);
            return result.ToHttpResult(response => Results.Ok(response.RepositoryNames));
        });

        app.MapGet("/repos/{name}/graph", async (
            string name,
            GetRepositoryGraphUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new GetRepositoryGraphCommand(name), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        });

        app.MapPost("/repos/scan", async (
            ScanDirectoryRequest request,
            IJobQueue jobQueue,
            IScanRepositoryJobFactory jobFactory,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.DirectoryPath))
            {
                return Results.BadRequest(new ErrorResult("El campo directoryPath es requerido."));
            }
            if (!Directory.Exists(request.DirectoryPath))
            {
                return Results.BadRequest(new ErrorResult($"El directorio no existe: {request.DirectoryPath}"));
            }
            var jobId = await jobQueue.EnqueueAsync(jobFactory.Create(request.DirectoryPath), cancellationToken);
            return Results.Accepted($"/jobs/{jobId}", new EnqueueResponse(jobId));
        });

        app.MapPost("/repos/cross-ref", async (
            BuildCrossRepoRefsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new BuildCrossRepoRefsCommand(), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        });

        app.MapGet("/repos/code-search", async (
            string? q,
            int? limit,
            string? modes,
            string? repos,
            string? kinds,
            SearchCodeDocumentsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(
                new SearchCodeDocumentsCommand(q, limit, modes, repos, kinds),
                cancellationToken);
            return result.ToHttpResult(response => Results.Ok(response.Results));
        });

        app.MapGet("/repos/file", async (
            string? path,
            GetCodeFileUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new GetCodeFileCommand(path), cancellationToken);
            return result.ToHttpResult(response =>
                Results.Text(response.Content, "text/plain; charset=utf-8"));
        });

        app.MapGet("/repos/search", async (
            string? q,
            int? depth,
            SearchCrossRepoSubgraphUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new SearchCrossRepoSubgraphCommand(q, depth), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        });

        app.MapGet("/repos/{name}/search", async (
            string name,
            string? q,
            int? depth,
            SearchRepositorySubgraphUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new SearchRepositorySubgraphCommand(name, q, depth), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        });
    }
}

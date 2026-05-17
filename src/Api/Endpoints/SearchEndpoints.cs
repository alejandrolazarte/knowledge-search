using KnowledgeSearch.Core.Domain.Search;
using KnowledgeSearch.Core.UseCases.Search;

namespace KnowledgeSearch;

internal static class SearchEndpoints
{
    public static void MapSearchRoutes(this WebApplication app)
    {
        app.MapGet("/search", async (
            SearchDocumentsUseCase useCase,
            string? q,
            int limit = 5,
            string? modes = null,
            string? roots = null,
            CancellationToken cancellationToken = default) =>
        {
            var result = await useCase.ExecuteAsync(
                new SearchDocumentsCommand(q, limit, modes, roots),
                cancellationToken);
            return result.ToHttpResult(response => Results.Ok(response.Results));
        });

        app.MapPost("/index", async (
            IndexDocumentsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new IndexDocumentsCommand(), cancellationToken);
            return result.ToHttpResult(response => Results.Ok(response.Result));
        });

        app.MapGet("/file", async (
            string? path,
            GetDocumentFileUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new GetDocumentFileCommand(path), cancellationToken);
            return result.ToHttpResult(response =>
                Results.Text(response.Content, "text/plain; charset=utf-8"));
        });

        app.MapPut("/file", async (
            string? path,
            HttpRequest request,
            SaveDocumentFileUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(request.Body);
            var content = await reader.ReadToEndAsync(cancellationToken);
            var result = await useCase.ExecuteAsync(
                new SaveDocumentFileCommand(path, content),
                cancellationToken);
            return result.ToHttpResult(response => Results.Ok(response));
        });

        app.MapGet("/image", async (
            string? path,
            GetImageFileUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new GetImageFileCommand(path), cancellationToken);
            return result.ToHttpResult(response => Results.File(response.Content, response.ContentType));
        });

        app.MapGet("/roots", async (
            GetKnowledgeRootsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new GetKnowledgeRootsCommand(), cancellationToken);
            return result.ToHttpResult(response => Results.Ok(response.Roots));
        });

        app.MapGet("/health", async (
            GetHealthUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new GetHealthCommand(), cancellationToken);
            return result.ToHttpResult(response => Results.Ok(response.Health));
        });
    }
}

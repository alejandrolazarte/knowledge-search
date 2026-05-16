using KnowledgeSearch.Core.Domain.Sources;
using KnowledgeSearch.Core.UseCases.Sources;

namespace KnowledgeSearch;

internal static class SourcesEndpoints
{
    public static void MapSourcesRoutes(this WebApplication app)
    {
        app.MapGet("/sources", async (
            GetSourcesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new GetSourcesCommand(), cancellationToken);
            return result.ToHttpResult(response => Results.Ok(response.Configuration));
        });

        app.MapPut("/sources", async (
            SourceConfigurationFile request,
            SaveSourcesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new SaveSourcesCommand(request), cancellationToken);
            return result.ToHttpResult(response => Results.Ok(response.Configuration));
        });

        app.MapGet("/sources/export", async (
            ExportSourcesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new ExportSourcesCommand(), cancellationToken);
            return result.ToHttpResult(response =>
                Results.Text(response.Json, "application/json; charset=utf-8"));
        });
    }
}

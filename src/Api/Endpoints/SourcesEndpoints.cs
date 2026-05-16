namespace KnowledgeSearch;

internal static class SourcesEndpoints
{
    public static void MapSourcesRoutes(this WebApplication app)
    {
        app.MapGet("/sources", (ISourceConfigurationService sources) =>
            Results.Ok(sources.GetConfiguration()));

        app.MapPut("/sources", (SourceConfigurationFile request, ISourceConfigurationService sources) =>
        {
            var result = sources.Save(request);
            return result.Success
                ? Results.Ok(sources.GetConfiguration())
                : Results.BadRequest(new ErrorResult(result.Error ?? "Configuración inválida."));
        });

        app.MapGet("/sources/export", (ISourceConfigurationService sources) =>
            Results.Text(sources.ExportJson(), "application/json; charset=utf-8"));
    }
}

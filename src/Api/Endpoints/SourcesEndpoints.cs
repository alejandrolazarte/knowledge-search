namespace KnowledgeSearch;

internal static class SourcesEndpoints
{
    public static void MapSourcesRoutes(this WebApplication app)
    {
        app.MapGet("/sources", (ISourceConfigurationService sources) =>
            Results.Ok(sources.GetConfiguration()));

        app.MapPut("/sources", (SourceConfigurationFile request, ISourceConfigurationService sources, IDbService dbService) =>
        {
            var result = sources.Save(request);
            if (!result.Success)
            {
                return Results.BadRequest(new ErrorResult(result.Error ?? "Configuración inválida."));
            }

            var savedConfig = sources.GetConfiguration();
            var newRoots = savedConfig.Sources
                .Select(s => s.ToConfiguredSource())
                .Where(s => s.IndexDocs)
                .Select(s => ConfiguredSource.ToAccessiblePath(s.HostPath))
                .ToArray();

            dbService.UpdateRoots(newRoots);

            return Results.Ok(savedConfig);
        });

        app.MapGet("/sources/export", (ISourceConfigurationService sources) =>
            Results.Text(sources.ExportJson(), "application/json; charset=utf-8"));
    }
}

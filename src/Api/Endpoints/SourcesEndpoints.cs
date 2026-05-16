namespace KnowledgeSearch;

internal static class SourcesEndpoints
{
    public static void MapSourcesRoutes(this WebApplication app)
    {
        app.MapGet("/sources", (ISourceConfigurationService sources) =>
            Results.Ok(sources.GetConfiguration()));

        app.MapPut("/sources", (
            SourceConfigurationFile request,
            ISourceConfigurationService sources,
            IDbService dbService,
            ICodeGraphService codeGraphService) =>
        {
            var result = sources.Save(request);
            if (!result.Success)
            {
                return Results.BadRequest(new ErrorResult(result.Error ?? "Configuración inválida."));
            }

            var savedConfig = sources.GetConfiguration();
            var configuredSources = savedConfig.Sources
                .Select(s => s.ToConfiguredSource())
                .ToArray();

            var docRoots = configuredSources
                .Where(s => s.IndexDocs)
                .Select(s => ConfiguredSource.ToAccessiblePath(s.HostPath))
                .ToArray();

            dbService.UpdateRoots(docRoots);

            foreach (var source in configuredSources.Where(s => s.IndexCode))
            {
                var accessiblePath = ConfiguredSource.ToAccessiblePath(source.HostPath);
                if (Directory.Exists(accessiblePath))
                {
                    codeGraphService.ScanDirectory(accessiblePath);
                }
            }

            return Results.Ok(savedConfig);
        });

        app.MapGet("/sources/export", (ISourceConfigurationService sources) =>
            Results.Text(sources.ExportJson(), "application/json; charset=utf-8"));
    }
}

using Microsoft.Data.Sqlite;

namespace KnowledgeSearch;

internal static class SearchEndpoints
{
    public static void MapSearchRoutes(this WebApplication app)
    {
        app.MapGet("/search", (IDbService dbService, string q, int limit = 5, string? modes = null,
            string? roots = null) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest("Falta parámetro q");
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
                return Results.BadRequest($"modes inválido: '{modes}'. Valores válidos: phrase, and, or");
            }

            var rootFilter = roots?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            try
            {
                var results = dbService.Search(q, limit, searchModes, rootFilter);
                return Results.Ok(results);
            }
            catch (SqliteException ex)
            {
                return Results.BadRequest($"Query inválida: {ex.Message}");
            }
        });

        app.MapPost("/index", (IDbService dbService) =>
        {
            var result = dbService.IndexDirectories();
            return Results.Ok(result);
        });

        app.MapGet("/file", (string path, IDbService dbService) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.BadRequest("Falta parámetro path");
            }

            var fullPath = Path.GetFullPath(path);

            if (!dbService.IsPathAllowed(fullPath))
            {
                return Results.BadRequest("Ruta fuera de los roots configurados");
            }

            if (!File.Exists(fullPath))
            {
                return Results.NotFound();
            }

            return Results.Text(File.ReadAllText(fullPath), "text/plain; charset=utf-8");
        });

        app.MapGet("/image", (string path, IDbService dbService) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.BadRequest("Falta parámetro path");
            }

            var fullPath = Path.GetFullPath(path);

            if (!dbService.IsPathAllowed(fullPath))
            {
                return Results.BadRequest("Ruta fuera de los roots configurados");
            }

            if (!File.Exists(fullPath))
            {
                return Results.NotFound();
            }

            var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
            {
                ".png"  => "image/png",
                ".jpg"  => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif"  => "image/gif",
                ".svg"  => "image/svg+xml",
                ".webp" => "image/webp",
                _       => "application/octet-stream",
            };

            return Results.File(File.ReadAllBytes(fullPath), contentType);
        });

        app.MapGet("/roots", (IDbService dbService) =>
            Results.Ok(dbService.GetRootNames()));

        app.MapGet("/health", () => Results.Ok(new HealthResult("ok")));
    }
}

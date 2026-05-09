namespace KnowledgeSearch;

static class StaticEndpoints
{
    public static void MapStaticRoutes(this WebApplication app, string indexHtmlPath, string staticDir)
    {
        app.MapGet("/", (HttpContext ctx) =>
        {
            ctx.Response.Headers["Cache-Control"] = "no-store";
            return File.Exists(indexHtmlPath)
                ? Results.Text(File.ReadAllText(indexHtmlPath), "text/html")
                : Results.NotFound("index.html no encontrado");
        });

        app.MapGet("/assets/{**path}", (string path) =>
        {
            var filePath = Path.Combine(staticDir, "assets", path);
            if (!File.Exists(filePath))
            {
                return Results.NotFound();
            }
            var mime = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".js"  => "application/javascript",
                ".css" => "text/css",
                ".map" => "application/json",
                _      => "application/octet-stream"
            };
            return Results.Text(File.ReadAllText(filePath), mime);
        });
    }
}

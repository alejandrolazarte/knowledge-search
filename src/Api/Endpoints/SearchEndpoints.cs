using Microsoft.Data.Sqlite;

namespace KnowledgeSearch;

internal static class SearchEndpoints
{
    public static void MapSearchRoutes(this WebApplication app, string dbPath, string docsDir)
    {
        app.MapGet("/search", (string q, int limit = 5) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest("Falta parámetro q");
            }

            using var connection = DbService.Open(dbPath);

            var results = new List<SearchResult>();
            using var cmd = new SqliteCommand(
                "SELECT title,section,path,line,content FROM docs WHERE docs MATCH @query ORDER BY bm25(docs,10,5,1) LIMIT @limit",
                connection);
            cmd.Parameters.AddWithValue("@query", DbService.BuildFtsQuery(q));
            cmd.Parameters.AddWithValue("@limit", limit);

            try
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new SearchResult(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt32(3),
                        reader.GetString(4)));
                }
            }
            catch (SqliteException ex)
            {
                return Results.BadRequest($"Query inválida: {ex.Message}");
            }

            return Results.Ok(results);
        });

        app.MapPost("/index", () =>
        {
            if (!Directory.Exists(docsDir))
            {
                return Results.BadRequest(new IndexResult(0, 0, 0));
            }

            using var connection = DbService.Open(dbPath);
            DbService.EnsureSchema(connection);

            var separator = Path.DirectorySeparatorChar;
            var files = Directory.EnumerateFiles(docsDir, "*.md",  SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(docsDir, "*.mkd", SearchOption.AllDirectories))
                .Select(Path.GetFullPath)
                .Where(f => !f.Contains($"{separator}node_modules{separator}") && !f.Contains($"{separator}.git{separator}"))
                .ToHashSet();

            var indexed = new HashSet<string>();
            using (var metaCmd = new SqliteCommand("SELECT path FROM docs_meta", connection))
            using (var metaReader = metaCmd.ExecuteReader())
            {
                while (metaReader.Read())
                {
                    indexed.Add(metaReader.GetString(0));
                }
            }

            int deleted = 0, added = 0, updated = 0;

            foreach (var removed in indexed.Except(files))
            {
                DbService.Execute(connection, "DELETE FROM docs      WHERE path=@path", removed);
                DbService.Execute(connection, "DELETE FROM docs_meta WHERE path=@path", removed);
                deleted++;
            }

            foreach (var file in files)
            {
                var modifiedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds();
                var stored     = DbService.QueryFirstLong(connection, "SELECT last_modified FROM docs_meta WHERE path=@path", file);

                if (stored == modifiedAt)
                {
                    continue;
                }

                var isNew = stored is null;
                if (!isNew)
                {
                    DbService.Execute(connection, "DELETE FROM docs WHERE path=@path", file);
                }

                DbService.IndexFile(connection, file, Path.GetFileNameWithoutExtension(file));
                DbService.Execute(connection,
                    "INSERT INTO docs_meta(path,last_modified) VALUES(@path,@modifiedAt) ON CONFLICT(path) DO UPDATE SET last_modified=excluded.last_modified",
                    file, modifiedAt);

                if (isNew)
                {
                    added++;
                }
                else
                {
                    updated++;
                }
            }

            return Results.Ok(new IndexResult(added, updated, deleted));
        });

        app.MapGet("/file", (string path) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.BadRequest("Falta parámetro path");
            }

            var fullPath = Path.GetFullPath(path);
            var docsRoot = Path.GetFullPath(docsDir);

            if (!fullPath.StartsWith(docsRoot, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest("Ruta fuera del knowledge dir");
            }

            if (!File.Exists(fullPath))
            {
                return Results.NotFound();
            }

            return Results.Text(File.ReadAllText(fullPath), "text/plain; charset=utf-8");
        });

        app.MapGet("/image", (string path) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.BadRequest("Falta parámetro path");
            }

            var fullPath = Path.GetFullPath(path);
            var docsRoot = Path.GetFullPath(docsDir);

            if (!fullPath.StartsWith(docsRoot, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest("Ruta fuera del knowledge dir");
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

        app.MapGet("/health", () => Results.Ok(new HealthResult("ok")));
    }
}

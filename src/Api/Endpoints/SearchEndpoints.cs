using Microsoft.Data.Sqlite;

namespace KnowledgeSearch;

static class SearchEndpoints
{
    public static void MapSearchRoutes(this WebApplication app, string dbPath, string docsDir)
    {
        app.MapGet("/search", (string q, int limit = 5) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.BadRequest("Falta parámetro q");

            using var con = DbService.Open(dbPath);

            var results = new List<SearchResult>();
            using var cmd = new SqliteCommand(
                "SELECT title,section,path,line,content FROM docs WHERE docs MATCH @q ORDER BY bm25(docs,10,5,1) LIMIT @lim",
                con);
            cmd.Parameters.AddWithValue("@q",   DbService.BuildFts(q));
            cmd.Parameters.AddWithValue("@lim", limit);

            try
            {
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    results.Add(new SearchResult(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetString(4)));
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
                return Results.BadRequest(new IndexResult(0, 0, 0));

            using var con = DbService.Open(dbPath);
            DbService.EnsureSchema(con);

            var sep   = Path.DirectorySeparatorChar;
            var files = Directory.EnumerateFiles(docsDir, "*.md",  SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(docsDir, "*.mkd", SearchOption.AllDirectories))
                .Select(Path.GetFullPath)
                .Where(f => !f.Contains($"{sep}node_modules{sep}") && !f.Contains($"{sep}.git{sep}"))
                .ToHashSet();

            var indexed = new HashSet<string>();
            using (var cmd = new SqliteCommand("SELECT path FROM docs_meta", con))
            using (var rd  = cmd.ExecuteReader())
                while (rd.Read()) indexed.Add(rd.GetString(0));

            int deleted = 0, added = 0, updated = 0;
            foreach (var removed in indexed.Except(files))
            {
                DbService.ExecP(con, "DELETE FROM docs      WHERE path=@p", removed);
                DbService.ExecP(con, "DELETE FROM docs_meta WHERE path=@p", removed);
                deleted++;
            }
            foreach (var file in files)
            {
                long  mtime  = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds();
                long? stored = DbService.QueryLong(con, "SELECT last_modified FROM docs_meta WHERE path=@p", file);
                if (stored == mtime) continue;

                bool isNew = stored is null;
                if (!isNew) DbService.ExecP(con, "DELETE FROM docs WHERE path=@p", file);

                DbService.IndexFile(con, file, Path.GetFileNameWithoutExtension(file));
                DbService.ExecP2(con,
                    "INSERT INTO docs_meta(path,last_modified) VALUES(@p,@m) ON CONFLICT(path) DO UPDATE SET last_modified=excluded.last_modified",
                    file, mtime);
                if (isNew) added++; else updated++;
            }

            return Results.Ok(new IndexResult(added, updated, deleted));
        });

        app.MapGet("/file", (string path) =>
        {
            if (string.IsNullOrWhiteSpace(path))
                return Results.BadRequest("Falta parámetro path");

            var fullPath = Path.GetFullPath(path);
            var docsRoot = Path.GetFullPath(docsDir);

            if (!fullPath.StartsWith(docsRoot, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Ruta fuera del knowledge dir");

            if (!File.Exists(fullPath))
                return Results.NotFound();

            var content = File.ReadAllText(fullPath);
            return Results.Text(content, "text/plain; charset=utf-8");
        });

        app.MapGet("/health", () => Results.Ok(new HealthResult("ok")));
    }
}

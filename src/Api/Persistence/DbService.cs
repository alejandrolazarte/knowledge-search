using Microsoft.Data.Sqlite;

namespace KnowledgeSearch;

internal static class DbService
{
    private const int SchemaVersion = 3;

    public static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        Execute(connection, "PRAGMA journal_mode=WAL");
        Execute(connection, "PRAGMA cache_size=-32000");
        Execute(connection, "PRAGMA synchronous=NORMAL");
        return connection;
    }

    public static void EnsureSchema(SqliteConnection connection)
    {
        Execute(connection, "CREATE TABLE IF NOT EXISTS schema_info(version INTEGER NOT NULL)");
        var version = QuerySchemaVersion(connection);

        if (version == SchemaVersion)
        {
            return;
        }

        Execute(connection, "DROP TABLE IF EXISTS docs");
        Execute(connection, "DROP TABLE IF EXISTS docs_meta");
        Execute(connection, """
            CREATE VIRTUAL TABLE docs USING fts5(
                title, section, content,
                path UNINDEXED, line UNINDEXED,
                tokenize='trigram')
            """);
        Execute(connection, "CREATE TABLE docs_meta(path TEXT PRIMARY KEY, last_modified INTEGER NOT NULL)");

        if (version is null)
        {
            Execute(connection, $"INSERT INTO schema_info(version) VALUES({SchemaVersion})");
        }
        else
        {
            Execute(connection, $"UPDATE schema_info SET version={SchemaVersion}");
        }
    }

    // ── Indexing ──────────────────────────────────────────────────────────

    public static void IndexFile(SqliteConnection connection, string path, string title)
    {
        var lines = File.ReadAllLines(path);
        var buffer = new List<string>();
        var start = 1;
        var section = title;

        void Flush()
        {
            if (buffer.Count == 0)
            {
                return;
            }

            if (buffer.All(l => string.IsNullOrWhiteSpace(l) || l.TrimStart().StartsWith('#')))
            {
                return;
            }

            using var cmd = new SqliteCommand(
                "INSERT INTO docs(title,section,content,path,line) VALUES(@title,@section,@content,@path,@line)", connection);
            cmd.Parameters.AddWithValue("@title",   title);
            cmd.Parameters.AddWithValue("@section", section);
            cmd.Parameters.AddWithValue("@content", string.Join("\n", buffer).Trim());
            cmd.Parameters.AddWithValue("@path",    path);
            cmd.Parameters.AddWithValue("@line",    start);
            cmd.ExecuteNonQuery();
            buffer.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('#'))
            {
                Flush();
                start   = i + 1;
                section = lines[i].TrimStart('#').Trim();
            }

            buffer.Add(lines[i]);
        }

        Flush();
    }

    public static string BuildFtsQuery(string query)
        => query.Trim().Contains(' ')
            ? string.Join(" OR ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => "\"" + w.Replace("\"", "\"\"") + "\""))
            : "\"" + query.Replace("\"", "\"\"") + "\"";

    // ── Helpers ────────────────────────────────────────────────────────────

    public static void Execute(SqliteConnection connection, string sql)
    {
        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    public static void Execute(SqliteConnection connection, string sql, string path)
    {
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@path", path);
        command.ExecuteNonQuery();
    }

    public static void Execute(SqliteConnection connection, string sql, string path, long modifiedAt)
    {
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@path",       path);
        command.Parameters.AddWithValue("@modifiedAt", modifiedAt);
        command.ExecuteNonQuery();
    }

    public static long? QueryFirstLong(SqliteConnection connection, string sql, string path)
    {
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@path", path);
        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : Convert.ToInt64(result);
    }

    private static long? QuerySchemaVersion(SqliteConnection connection)
    {
        using var command = new SqliteCommand("SELECT version FROM schema_info LIMIT 1", connection);
        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : Convert.ToInt64(result);
    }
}

using Microsoft.Data.Sqlite;

namespace KnowledgeSearch;

static class DbService
{
    const int SchemaVersion = 3;

    public static SqliteConnection Open(string path)
    {
        var con = new SqliteConnection($"Data Source={path}");
        con.Open();
        Exec(con, "PRAGMA journal_mode=WAL");
        Exec(con, "PRAGMA cache_size=-32000");
        Exec(con, "PRAGMA synchronous=NORMAL");
        return con;
    }

    public static void EnsureSchema(SqliteConnection con)
    {
        Exec(con, "CREATE TABLE IF NOT EXISTS schema_info(version INTEGER NOT NULL)");
        long? version = QueryScalar(con, "SELECT version FROM schema_info LIMIT 1");

        if (version == SchemaVersion) return;

        Exec(con, "DROP TABLE IF EXISTS docs");
        Exec(con, "DROP TABLE IF EXISTS docs_meta");
        Exec(con, """
            CREATE VIRTUAL TABLE docs USING fts5(
                title, section, content,
                path UNINDEXED, line UNINDEXED,
                tokenize='trigram')
            """);
        Exec(con, "CREATE TABLE docs_meta(path TEXT PRIMARY KEY, last_modified INTEGER NOT NULL)");

        if (version is null)
            Exec(con, $"INSERT INTO schema_info(version) VALUES({SchemaVersion})");
        else
            Exec(con, $"UPDATE schema_info SET version={SchemaVersion}");
    }

    // ── Indexing ──────────────────────────────────────────────────────────

    public static void IndexFile(SqliteConnection con, string path, string title)
    {
        var lines   = File.ReadAllLines(path);
        var buffer  = new List<string>();
        int start   = 1;
        string section = title;

        void Flush()
        {
            if (buffer.Count == 0) return;
            if (buffer.All(l => string.IsNullOrWhiteSpace(l) || l.TrimStart().StartsWith('#'))) return;

            using var cmd = new SqliteCommand(
                "INSERT INTO docs(title,section,content,path,line) VALUES(@t,@s,@c,@p,@l)", con);
            cmd.Parameters.AddWithValue("@t", title);
            cmd.Parameters.AddWithValue("@s", section);
            cmd.Parameters.AddWithValue("@c", string.Join("\n", buffer).Trim());
            cmd.Parameters.AddWithValue("@p", path);
            cmd.Parameters.AddWithValue("@l", start);
            cmd.ExecuteNonQuery();
            buffer.Clear();
        }

        for (int i = 0; i < lines.Length; i++)
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

    public static string BuildFts(string q)
        => q.Trim().Contains(' ')
            ? string.Join(" OR ", q.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => "\"" + w.Replace("\"", "\"\"") + "\""))
            : "\"" + q.Replace("\"", "\"\"") + "\"";

    // ── Low-level helpers ─────────────────────────────────────────────────

    public static void Exec(SqliteConnection con, string sql)
    {
        using var c = new SqliteCommand(sql, con);
        c.ExecuteNonQuery();
    }

    public static void ExecP(SqliteConnection con, string sql, string p)
    {
        using var c = new SqliteCommand(sql, con);
        c.Parameters.AddWithValue("@p", p);
        c.ExecuteNonQuery();
    }

    public static void ExecP2(SqliteConnection con, string sql, string p, long m)
    {
        using var c = new SqliteCommand(sql, con);
        c.Parameters.AddWithValue("@p", p);
        c.Parameters.AddWithValue("@m", m);
        c.ExecuteNonQuery();
    }

    public static long? QueryLong(SqliteConnection con, string sql, string p)
    {
        using var c = new SqliteCommand(sql, con);
        c.Parameters.AddWithValue("@p", p);
        var r = c.ExecuteScalar();
        return r is null or DBNull ? null : Convert.ToInt64(r);
    }

    public static long? QueryScalar(SqliteConnection con, string sql)
    {
        using var c = new SqliteCommand(sql, con);
        var r = c.ExecuteScalar();
        return r is null or DBNull ? null : Convert.ToInt64(r);
    }
}

using Microsoft.Data.Sqlite;

namespace KnowledgeSearch;

internal sealed class CodeGraphRepository : ICodeGraphRepository
{
    private const string DeleteNodesSql = "DELETE FROM code_nodes WHERE repo_name = @repoName";
    private const string DeleteEdgesSql = "DELETE FROM code_edges WHERE repo_name = @repoName";
    private const string UpsertRepoSql =
        "INSERT INTO code_repos(name, last_scanned) VALUES(@name, @lastScanned) " +
        "ON CONFLICT(name) DO UPDATE SET last_scanned = excluded.last_scanned";
    private const string InsertNodeSql =
        "INSERT INTO code_nodes(repo_name, identifier, name, kind, file_path, line) " +
        "VALUES(@repoName, @identifier, @name, @kind, @filePath, @line)";
    private const string InsertEdgeSql =
        "INSERT INTO code_edges(repo_name, source_identifier, target_identifier, kind, line) " +
        "VALUES(@repoName, @sourceIdentifier, @targetIdentifier, @kind, @line)";
    private const string SelectNodesSql =
        "SELECT identifier, name, kind, file_path, line FROM code_nodes WHERE repo_name = @repoName";
    private const string SelectEdgesSql =
        "SELECT source_identifier, target_identifier, kind, line FROM code_edges WHERE repo_name = @repoName";
    private const string RepositoryExistsSql =
        "SELECT COUNT(1) FROM code_repos WHERE name = @name";
    private const string SelectRepoNamesSql =
        "SELECT name FROM code_repos ORDER BY name";

    private readonly SqliteConnection _connection;

    public CodeGraphRepository(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        ApplyPragmas();
        EnsureSchema();
    }

    public void SaveScanResult(string repositoryName, CodeGraphScanResult scanResult)
    {
        using var transaction = _connection.BeginTransaction();
        try
        {
            DeleteExistingData(repositoryName, transaction);
            UpsertRepository(repositoryName, transaction);
            InsertNodes(repositoryName, scanResult.Nodes, transaction);
            InsertEdges(repositoryName, scanResult.Edges, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public bool RepositoryExists(string repositoryName)
    {
        using var command = new SqliteCommand(RepositoryExistsSql, _connection);
        command.Parameters.AddWithValue("@name", repositoryName);
        return Convert.ToInt64(command.ExecuteScalar()!, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    public IReadOnlyList<CodeNode> GetNodes(string repositoryName)
    {
        var nodes = new List<CodeNode>();
        using var command = new SqliteCommand(SelectNodesSql, _connection);
        command.Parameters.AddWithValue("@repoName", repositoryName);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            nodes.Add(new CodeNode(
                reader.GetString(0),
                reader.GetString(1),
                Enum.Parse<CodeNodeKind>(reader.GetString(2)),
                reader.GetString(3),
                reader.GetInt32(4)));
        }

        return nodes;
    }

    public IReadOnlyList<CodeEdge> GetEdges(string repositoryName)
    {
        var edges = new List<CodeEdge>();
        using var command = new SqliteCommand(SelectEdgesSql, _connection);
        command.Parameters.AddWithValue("@repoName", repositoryName);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            edges.Add(new CodeEdge(
                reader.GetString(0),
                reader.GetString(1),
                Enum.Parse<CodeEdgeKind>(reader.GetString(2)),
                reader.GetInt32(3)));
        }

        return edges;
    }

    public IReadOnlyList<string> GetRepositoryNames()
    {
        var names = new List<string>();
        using var command = new SqliteCommand(SelectRepoNamesSql, _connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        GC.SuppressFinalize(this);
    }

    private void DeleteExistingData(string repositoryName, SqliteTransaction transaction)
    {
        using var deleteNodes = new SqliteCommand(DeleteNodesSql, _connection, transaction);
        deleteNodes.Parameters.AddWithValue("@repoName", repositoryName);
        deleteNodes.ExecuteNonQuery();

        using var deleteEdges = new SqliteCommand(DeleteEdgesSql, _connection, transaction);
        deleteEdges.Parameters.AddWithValue("@repoName", repositoryName);
        deleteEdges.ExecuteNonQuery();
    }

    private void UpsertRepository(string repositoryName, SqliteTransaction transaction)
    {
        using var command = new SqliteCommand(UpsertRepoSql, _connection, transaction);
        command.Parameters.AddWithValue("@name", repositoryName);
        command.Parameters.AddWithValue("@lastScanned", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        command.ExecuteNonQuery();
    }

    private void InsertNodes(string repositoryName, IReadOnlyList<CodeNode> nodes, SqliteTransaction transaction)
    {
        foreach (var node in nodes)
        {
            using var command = new SqliteCommand(InsertNodeSql, _connection, transaction);
            command.Parameters.AddWithValue("@repoName", repositoryName);
            command.Parameters.AddWithValue("@identifier", node.Identifier);
            command.Parameters.AddWithValue("@name", node.Name);
            command.Parameters.AddWithValue("@kind", node.Kind.ToString());
            command.Parameters.AddWithValue("@filePath", node.FilePath);
            command.Parameters.AddWithValue("@line", node.Line);
            command.ExecuteNonQuery();
        }
    }

    private void InsertEdges(string repositoryName, IReadOnlyList<CodeEdge> edges, SqliteTransaction transaction)
    {
        foreach (var edge in edges)
        {
            using var command = new SqliteCommand(InsertEdgeSql, _connection, transaction);
            command.Parameters.AddWithValue("@repoName", repositoryName);
            command.Parameters.AddWithValue("@sourceIdentifier", edge.SourceIdentifier);
            command.Parameters.AddWithValue("@targetIdentifier", edge.TargetIdentifier);
            command.Parameters.AddWithValue("@kind", edge.Kind.ToString());
            command.Parameters.AddWithValue("@line", edge.Line);
            command.ExecuteNonQuery();
        }
    }

    private void ApplyPragmas()
    {
        ExecuteSql("PRAGMA journal_mode=WAL");
        ExecuteSql("PRAGMA cache_size=-8000");
        ExecuteSql("PRAGMA synchronous=NORMAL");
    }

    private void EnsureSchema()
    {
        ExecuteSql("""
            CREATE TABLE IF NOT EXISTS code_repos (
                name TEXT PRIMARY KEY,
                last_scanned INTEGER NOT NULL
            )
            """);
        ExecuteSql("""
            CREATE TABLE IF NOT EXISTS code_nodes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                repo_name TEXT NOT NULL,
                identifier TEXT NOT NULL,
                name TEXT NOT NULL,
                kind TEXT NOT NULL,
                file_path TEXT NOT NULL,
                line INTEGER NOT NULL,
                FOREIGN KEY(repo_name) REFERENCES code_repos(name)
            )
            """);
        ExecuteSql("""
            CREATE TABLE IF NOT EXISTS code_edges (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                repo_name TEXT NOT NULL,
                source_identifier TEXT NOT NULL,
                target_identifier TEXT NOT NULL,
                kind TEXT NOT NULL,
                line INTEGER NOT NULL,
                FOREIGN KEY(repo_name) REFERENCES code_repos(name)
            )
            """);
        ExecuteSql("CREATE INDEX IF NOT EXISTS idx_code_nodes_repo ON code_nodes(repo_name)");
        ExecuteSql("CREATE INDEX IF NOT EXISTS idx_code_edges_repo ON code_edges(repo_name)");
    }

    private void ExecuteSql(string sql)
    {
        using var command = new SqliteCommand(sql, _connection);
        command.ExecuteNonQuery();
    }
}

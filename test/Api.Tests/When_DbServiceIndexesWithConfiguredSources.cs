using KnowledgeSearch;
using KnowledgeSearch.Core.Domain.Sources;
using Shouldly;
using Xunit;

namespace Api.Tests;

// DbService.IndexDirectories debe respetar los excludes del ConfiguredSource y
// las exclusiones por defecto (node_modules, bin, .git, etc.). Antes del
// walker recursivo el filtro era hardcoded a "node_modules" y ".git" y ademas
// se hacia post-enumeracion (entraba a las ramas excluidas igual).
public class When_DbServiceIndexesWithConfiguredSources : IDisposable
{
    private readonly string _root   = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _dbPath = Path.GetTempFileName();

    public When_DbServiceIndexesWithConfiguredSources() => Directory.CreateDirectory(_root);

    [Fact]
    public void Then_DoesNotIndexMarkdownUnderConfiguredExcludeGlobs()
    {
        WriteMarkdown("docs/Real.md",          "# Real\nfindable needle");
        WriteMarkdown("sandbox/Throwaway.md",  "# Throwaway\nfindable needle");
        WriteMarkdown("reports/q1/Stale.md",   "# Stale\nfindable needle");

        var source = ConfiguredSource.Create(
            id:       "knowledge",
            name:     "knowledge",
            kind:     SourceKind.Knowledge,
            hostPath: _root,
            excludes: ["**/sandbox/**", "**/reports/**"]);

        using var sut = new DbService(_dbPath, [source]);
        sut.IndexDirectories();

        var hits = sut.Search("findable needle", 10);
        hits.Select(h => h.Title).OrderBy(t => t).ShouldBe(["Real"]);
    }

    [Fact]
    public void Then_SkipsDefaultBlocklistDirectoriesEvenWhenSourceDoesNotListThem()
    {
        WriteMarkdown("docs/Real.md",          "# Real\nfindable needle");
        WriteMarkdown("node_modules/pkg/Doc.md","# Junk\nfindable needle");
        WriteMarkdown("bin/Debug/README.md",   "# Debug\nfindable needle");

        var source = ConfiguredSource.Create(
            id:       "knowledge",
            name:     "knowledge",
            kind:     SourceKind.Knowledge,
            hostPath: _root);

        using var sut = new DbService(_dbPath, [source]);
        sut.IndexDirectories();

        var hits = sut.Search("findable needle", 10);
        hits.Select(h => h.Title).OrderBy(t => t).ShouldBe(["Real"]);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch { }
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        GC.SuppressFinalize(this);
    }

    private void WriteMarkdown(string relativePath, string content)
    {
        var fullPath = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}

using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_DbServiceSearches : IDisposable
{
    private readonly string _docsDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _dbPath = Path.GetTempFileName();
    private readonly DbService _sut;

    public When_DbServiceSearches()
    {
        Directory.CreateDirectory(_docsDir);
        _sut = new DbService(_dbPath, new[] { _docsDir });
    }

    [Fact]
    public void Then_PhraseSearchReturnsMatchingResult()
    {
        var filePath = Path.Combine(_docsDir, "guide.md");
        File.WriteAllText(filePath, "# Getting Started\nThis is a getting started guide.");
        _sut.IndexDirectories();

        var results = _sut.Search("getting started", 5);

        results.ShouldHaveSingleItem();
        results[0].Title.ShouldBe("guide");
        results[0].Root.ShouldBe(Path.GetFileName(_docsDir));
    }

    [Fact]
    public void Then_IsPathAllowedReturnsTrueForPathInsideRoot()
    {
        var path = Path.Combine(_docsDir, "subdir", "file.md");

        _sut.IsPathAllowed(path).ShouldBeTrue();
    }

    [Fact]
    public void Then_IsPathAllowedReturnsFalseForPathOutsideRoot()
    {
        _sut.IsPathAllowed(@"C:\Windows\System32\evil.txt").ShouldBeFalse();
    }

    [Fact]
    public void Then_IndexDirectoriesWithNonExistentRootDoesNotThrow()
    {
        var nonExistentRoot = Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid());
        using var sut = new DbService(_dbPath, new[] { nonExistentRoot });

        var result = sut.IndexDirectories();

        result.Added.ShouldBe(0);
        result.Updated.ShouldBe(0);
        result.Deleted.ShouldBe(0);
    }

    [Fact]
    public void Then_DuplicateRootNamesReceiveDistinctLabelsAndCanBeFiltered()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var dbPath = Path.GetTempFileName();

        try
        {
            var firstRoot = Path.Combine(baseDir, "alpha", "docs");
            var secondRoot = Path.Combine(baseDir, "beta", "docs");
            Directory.CreateDirectory(firstRoot);
            Directory.CreateDirectory(secondRoot);
            File.WriteAllText(Path.Combine(firstRoot, "first.md"), "# First\nshared needle first");
            File.WriteAllText(Path.Combine(secondRoot, "second.md"), "# Second\nshared needle second");

            using var sut = new DbService(dbPath, [firstRoot, secondRoot]);
            sut.IndexDirectories();

            var roots = sut.GetRootNames();
            roots.ShouldBe(["alpha/docs", "beta/docs"]);

            var results = sut.Search("shared needle", 5, roots: ["beta/docs"]);

            results.ShouldHaveSingleItem();
            results[0].Root.ShouldBe("beta/docs");
            results[0].Title.ShouldBe("second");
        }
        finally
        {
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, recursive: true);
            }
            File.Delete(dbPath);
        }
    }

    public void Dispose()
    {
        _sut.Dispose();
        Directory.Delete(_docsDir, recursive: true);
        File.Delete(_dbPath);
        GC.SuppressFinalize(this);
    }
}

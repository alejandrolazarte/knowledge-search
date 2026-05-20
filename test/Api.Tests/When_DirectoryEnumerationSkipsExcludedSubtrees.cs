using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Verifica que el <see cref="RecursiveDirectoryWalker"/> corta las ramas
/// excluidas antes de descender — a diferencia de
/// <c>Directory.EnumerateFiles(root, "*", AllDirectories)</c>, que recorre el
/// arbol entero (incluso <c>node_modules</c>) y filtra despues.
/// </summary>
public class When_DirectoryEnumerationSkipsExcludedSubtrees : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public When_DirectoryEnumerationSkipsExcludedSubtrees() => Directory.CreateDirectory(_root);

    [Fact]
    public void Then_ReturnsOnlyFilesMatchingIncludedExtensions()
    {
        WriteFile("src/Service.cs", "namespace A; public class S {}");
        WriteFile("src/Component.tsx", "export const C = () => null;");
        WriteFile("src/readme.md", "# title");
        WriteFile("src/image.png", "binary");

        var walker = new RecursiveDirectoryWalker();
        var result = walker.Enumerate(_root, new DirectoryWalkOptions(
            IncludeExtensions: [".cs", ".tsx"],
            ExcludedDirectoryNames: [],
            ExcludeGlobs: []));

        result.Files.Select(Path.GetFileName).OrderBy(n => n).ShouldBe(["Component.tsx", "Service.cs"]);
    }

    [Fact]
    public void Then_DoesNotDescendIntoExcludedDirectory()
    {
        WriteFile("src/Real.cs", "namespace A; public class R {}");

        WriteFile("node_modules/a/b/c/junk1.cs", "junk");
        WriteFile("node_modules/a/b/c/junk2.cs", "junk");
        WriteFile("node_modules/a/b/junk3.cs",  "junk");
        WriteFile("node_modules/a/junk4.cs",    "junk");

        var walker = new RecursiveDirectoryWalker();
        var result = walker.Enumerate(_root, new DirectoryWalkOptions(
            IncludeExtensions: [".cs"],
            ExcludedDirectoryNames: ["node_modules"],
            ExcludeGlobs: []));

        result.Files.Select(Path.GetFileName).ShouldBe(["Real.cs"]);

        const int rootPlusSrcOnly = 2;
        result.DirectoriesDescended.ShouldBe(rootPlusSrcOnly);
    }

    [Fact]
    public void Then_HonorsMultipleExcludedDirectoryNamesByDefault()
    {
        WriteFile("src/Real.cs", "namespace A; public class R {}");
        WriteFile("bin/Debug/Compiled.cs", "junk");
        WriteFile("obj/cache.cs",           "junk");
        WriteFile(".git/HEAD",              "ref");

        var walker = new RecursiveDirectoryWalker();
        var result = walker.Enumerate(_root, new DirectoryWalkOptions(
            IncludeExtensions: [".cs"],
            ExcludedDirectoryNames: ["bin", "obj", ".git"],
            ExcludeGlobs: []));

        result.Files.Select(Path.GetFileName).ShouldBe(["Real.cs"]);
    }

    [Fact]
    public void Then_HonorsExcludeGlobRelativeToRoot()
    {
        WriteFile("src/Real.cs",         "namespace A; public class R {}");
        WriteFile("sandbox/Throwaway.cs", "junk");
        WriteFile("reports/q1/Report.cs", "junk");

        var walker = new RecursiveDirectoryWalker();
        var result = walker.Enumerate(_root, new DirectoryWalkOptions(
            IncludeExtensions: [".cs"],
            ExcludedDirectoryNames: [],
            ExcludeGlobs: ["**/sandbox/**", "**/reports/**"]));

        result.Files.Select(Path.GetFileName).ShouldBe(["Real.cs"]);
    }

    [Fact]
    public void Then_DoesNotCountExcludedRootInDescendedDirectories()
    {
        WriteFile("src/Real.cs",                 "namespace A; public class R {}");
        WriteFile("node_modules/a/b/junk.cs",     "junk");
        WriteFile("node_modules/c/d/e/junk2.cs",  "junk");

        var walker = new RecursiveDirectoryWalker();
        var result = walker.Enumerate(_root, new DirectoryWalkOptions(
            IncludeExtensions: [".cs"],
            ExcludedDirectoryNames: ["node_modules"],
            ExcludeGlobs: []));

        const int rootPlusSrcOnly = 2;
        result.DirectoriesDescended.ShouldBe(rootPlusSrcOnly);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_root, relativePath);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
    }
}

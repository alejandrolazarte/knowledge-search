using KnowledgeSearch;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_CodeGraphServiceScans : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    private CodeGraphRepository? _repository;

    public When_CodeGraphServiceScans() => Directory.CreateDirectory(_temporaryDirectory);

    [Fact]
    public void Then_ParsesCSharpFilesUsingCSharpParser()
    {
        WriteSourceFile("MyService.cs", """
            namespace MyApp;
            public class MyService { }
            """);

        var result = BuildService([".cs"]).ScanDirectory(_temporaryDirectory);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Class && n.Name == "MyService");
    }

    [Fact]
    public void Then_ParsesTypeScriptFilesUsingTypeScriptParser()
    {
        WriteSourceFile("service.ts", """
            export class UserService { }
            """);

        var result = BuildService([".ts"]).ScanDirectory(_temporaryDirectory);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Class && n.Name == "UserService");
    }

    [Fact]
    public void Then_ParsesPythonFilesUsingPythonParser()
    {
        WriteSourceFile("service.py", """
            class UserService:
                pass
            """);

        var result = BuildService([".py"]).ScanDirectory(_temporaryDirectory);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Class && n.Name == "UserService");
    }

    [Fact]
    public void Then_SkipsFilesWithNoRegisteredParser()
    {
        WriteSourceFile("script.rb", "class MyClass; end");

        var result = BuildService([".cs"]).ScanDirectory(_temporaryDirectory);

        result.FilesScanned.ShouldBe(0);
        result.FilesSkipped.ShouldBe(1);
    }

    [Fact]
    public void Then_ScansMultipleLanguagesInSameDirectory()
    {
        WriteSourceFile("service.cs", """
            namespace MyApp;
            public class CSharpService { }
            """);
        WriteSourceFile("service.ts", "export class TypeScriptService { }");

        var result = BuildService([".cs", ".ts"]).ScanDirectory(_temporaryDirectory);

        result.Nodes.ShouldContain(n => n.Name == "CSharpService");
        result.Nodes.ShouldContain(n => n.Name == "TypeScriptService");
        result.FilesScanned.ShouldBe(2);
    }

    [Fact]
    public void Then_ExcludesNodeModulesDirectory()
    {
        var nodeModulesPath = Path.Combine(_temporaryDirectory, "node_modules");
        Directory.CreateDirectory(nodeModulesPath);
        WriteSourceFile(Path.Combine("node_modules", "lib.ts"), "export class ShouldBeIgnored { }");

        var result = BuildService([".ts"]).ScanDirectory(_temporaryDirectory);

        result.Nodes.ShouldNotContain(n => n.Name == "ShouldBeIgnored");
    }

    [Fact]
    public void Then_ExcludesBinAndObjDirectories()
    {
        var binPath = Path.Combine(_temporaryDirectory, "bin");
        var objPath = Path.Combine(_temporaryDirectory, "obj");
        Directory.CreateDirectory(binPath);
        Directory.CreateDirectory(objPath);
        WriteSourceFile(Path.Combine("bin", "Generated.cs"), "public class ShouldBeIgnored { }");
        WriteSourceFile(Path.Combine("obj", "Compiled.cs"), "public class AlsoIgnored { }");

        var result = BuildService([".cs"]).ScanDirectory(_temporaryDirectory);

        result.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public void Then_DeduplicatesNodesWithSameIdentifier()
    {
        WriteSourceFile("Partial1.cs", """
            namespace MyApp;
            public partial class ConfigureServices { }
            """);
        WriteSourceFile("Partial2.cs", """
            namespace MyApp;
            public partial class ConfigureServices { }
            """);

        BuildService([".cs"]).ScanDirectory(_temporaryDirectory);

        _repository!.GetNodes(Path.GetFileName(_temporaryDirectory))
            .ShouldHaveSingleItem()
            .Identifier.ShouldBe("MyApp.ConfigureServices");
    }

    public void Dispose()
    {
        _repository?.Dispose();
        SqliteConnection.ClearAllPools();

        try { Directory.Delete(_temporaryDirectory, recursive: true); } catch { }
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        GC.SuppressFinalize(this);
    }

    private static readonly string[] TypeScriptExtensions = [".ts", ".tsx", ".js", ".jsx", ".mjs"];

    private CodeGraphService BuildService(IReadOnlyList<string> extensions)
    {
        var services = new ServiceCollection();

        if (extensions.Contains(".cs"))
        {
            services.AddKeyedSingleton<ISourceFileParser, CSharpParser>(".cs");
        }

        foreach (var extension in TypeScriptExtensions.Where(extensions.Contains))
        {
            services.AddKeyedSingleton<ISourceFileParser, TypeScriptParser>(extension);
        }

        if (extensions.Contains(".py"))
        {
            services.AddKeyedSingleton<ISourceFileParser, PythonParser>(".py");
        }

        _repository?.Dispose();
        _repository = new CodeGraphRepository(_dbPath);
        return new CodeGraphService(services.BuildServiceProvider(), _repository);
    }

    private void WriteSourceFile(string relativePath, string sourceCode)
    {
        var fullPath = Path.Combine(_temporaryDirectory, relativePath);
        File.WriteAllText(fullPath, sourceCode);
    }
}

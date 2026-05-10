using KnowledgeSearch;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_CodeGraphServiceScans : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

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

    public void Dispose()
    {
        Directory.Delete(_temporaryDirectory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static readonly string[] TypeScriptExtensions = [".ts", ".tsx", ".js", ".jsx", ".mjs"];

    private static CodeGraphService BuildService(IReadOnlyList<string> extensions)
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

        return new CodeGraphService(services.BuildServiceProvider());
    }

    private void WriteSourceFile(string relativePath, string sourceCode)
    {
        var fullPath = Path.Combine(_temporaryDirectory, relativePath);
        File.WriteAllText(fullPath, sourceCode);
    }
}

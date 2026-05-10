using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_CSharpParserExtracts : IDisposable
{
    private readonly CSharpParser _sut = new();
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public When_CSharpParserExtracts() => Directory.CreateDirectory(_temporaryDirectory);

    [Fact]
    public void Then_CanParseReturnsTrueForCSharpFiles() =>
        _sut.CanParse("MyClass.cs").ShouldBeTrue();

    [Fact]
    public void Then_CanParseReturnsFalseForNonCSharpFiles() =>
        _sut.CanParse("index.ts").ShouldBeFalse();

    [Fact]
    public void Then_ExtractsClassDeclaration()
    {
        var sourceFilePath = WriteSourceFile("MyClass.cs", """
            namespace MyApp;
            public class MyClass { }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Class && n.Name == "MyClass");
    }

    [Fact]
    public void Then_ExtractsInterfaceDeclaration()
    {
        var sourceFilePath = WriteSourceFile("IMyService.cs", """
            namespace MyApp;
            public interface IMyService { }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Interface && n.Name == "IMyService");
    }

    [Fact]
    public void Then_ExtractsRecordDeclaration()
    {
        var sourceFilePath = WriteSourceFile("MyRecord.cs", """
            namespace MyApp;
            public record MyRecord(string Name);
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Record && n.Name == "MyRecord");
    }

    [Fact]
    public void Then_ExtractsEnumDeclaration()
    {
        var sourceFilePath = WriteSourceFile("MyEnum.cs", """
            namespace MyApp;
            public enum MyEnum { Alpha, Beta }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Enum && n.Name == "MyEnum");
    }

    [Fact]
    public void Then_ExtractsMethodDeclarations()
    {
        var sourceFilePath = WriteSourceFile("MyService.cs", """
            namespace MyApp;
            public class MyService
            {
                public void Execute() { }
                public string GetName() => "name";
            }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Method && n.Name == "Execute");
        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Method && n.Name == "GetName");
    }

    [Fact]
    public void Then_NodeIdentifierIsFullyQualifiedWithNamespace()
    {
        var sourceFilePath = WriteSourceFile("MyClass.cs", """
            namespace MyApp.Core;
            public class MyClass { }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Identifier == "MyApp.Core.MyClass");
    }

    [Fact]
    public void Then_ExtractsContainsEdgeFromClassToMethod()
    {
        var sourceFilePath = WriteSourceFile("MyService.cs", """
            namespace MyApp;
            public class MyService
            {
                public void Execute() { }
            }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Edges.ShouldContain(e =>
            e.SourceIdentifier == "MyApp.MyService" &&
            e.TargetIdentifier == "MyApp.MyService.Execute" &&
            e.Kind == CodeEdgeKind.Contains);
    }

    [Fact]
    public void Then_ExtractsInheritsEdgeFromClassToBaseClass()
    {
        var sourceFilePath = WriteSourceFile("MyHandler.cs", """
            namespace MyApp;
            public class MyHandler : BaseHandler { }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Edges.ShouldContain(e =>
            e.SourceIdentifier == "MyApp.MyHandler" &&
            e.TargetIdentifier == "BaseHandler" &&
            e.Kind == CodeEdgeKind.Inherits);
    }

    [Fact]
    public void Then_ExtractsImplementsEdgeFromClassToInterface()
    {
        var sourceFilePath = WriteSourceFile("MyService.cs", """
            namespace MyApp;
            public class MyService : IMyService { }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Edges.ShouldContain(e =>
            e.SourceIdentifier == "MyApp.MyService" &&
            e.TargetIdentifier == "IMyService" &&
            e.Kind == CodeEdgeKind.Implements);
    }

    [Fact]
    public void Then_ReturnsEmptyResultForEmptyFile()
    {
        var sourceFilePath = WriteSourceFile("Empty.cs", string.Empty);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldBeEmpty();
        result.Edges.ShouldBeEmpty();
    }

    public void Dispose()
    {
        Directory.Delete(_temporaryDirectory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string WriteSourceFile(string fileName, string sourceCode)
    {
        var filePath = Path.Combine(_temporaryDirectory, fileName);
        File.WriteAllText(filePath, sourceCode);
        return filePath;
    }
}

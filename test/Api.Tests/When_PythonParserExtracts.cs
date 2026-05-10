using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_PythonParserExtracts : IDisposable
{
    private readonly PythonParser _sut = new();
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public When_PythonParserExtracts() => Directory.CreateDirectory(_temporaryDirectory);

    [Fact]
    public void Then_ExtractsClassDeclaration()
    {
        var sourceFilePath = WriteSourceFile("service.py", """
            class UserService:
                pass
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Class && n.Name == "UserService");
    }

    [Fact]
    public void Then_ExtractsTopLevelFunctionDeclaration()
    {
        var sourceFilePath = WriteSourceFile("utils.py", """
            def calculate_total(items):
                return sum(items)
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Method && n.Name == "calculate_total");
    }

    [Fact]
    public void Then_ExtractsMethodsInsideClass()
    {
        var sourceFilePath = WriteSourceFile("service.py", """
            class UserService:
                def get_user(self, user_id: str) -> str:
                    return user_id

                def save_user(self, user: dict) -> None:
                    pass
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Method && n.Name == "get_user");
        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Method && n.Name == "save_user");
    }

    [Fact]
    public void Then_ExtractsContainsEdgeFromClassToMethod()
    {
        var sourceFilePath = WriteSourceFile("service.py", """
            class UserService:
                def get_user(self, user_id: str) -> str:
                    return user_id
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Edges.ShouldContain(e =>
            e.SourceIdentifier == "UserService" &&
            e.TargetIdentifier == "UserService.get_user" &&
            e.Kind == CodeEdgeKind.Contains);
    }

    [Fact]
    public void Then_ExtractsInheritsEdgeFromBaseClass()
    {
        var sourceFilePath = WriteSourceFile("handler.py", """
            class AdminHandler(BaseHandler):
                pass
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Edges.ShouldContain(e =>
            e.SourceIdentifier == "AdminHandler" &&
            e.TargetIdentifier == "BaseHandler" &&
            e.Kind == CodeEdgeKind.Inherits);
    }

    [Fact]
    public void Then_ExtractsImportEdgeFromImportStatement()
    {
        var sourceFilePath = WriteSourceFile("service.py", """
            import os
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Edges.ShouldContain(e =>
            e.TargetIdentifier == "os" &&
            e.Kind == CodeEdgeKind.Imports);
    }

    [Fact]
    public void Then_ExtractsImportEdgeFromFromImportStatement()
    {
        var sourceFilePath = WriteSourceFile("service.py", """
            from typing import List
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Edges.ShouldContain(e =>
            e.TargetIdentifier == "typing" &&
            e.Kind == CodeEdgeKind.Imports);
    }

    [Fact]
    public void Then_ReturnsEmptyResultForEmptyFile()
    {
        var sourceFilePath = WriteSourceFile("empty.py", string.Empty);

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

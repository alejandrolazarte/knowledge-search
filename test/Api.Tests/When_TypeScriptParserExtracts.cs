using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_TypeScriptParserExtracts : IDisposable
{
    private readonly TypeScriptParser _sut = new();
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public When_TypeScriptParserExtracts() => Directory.CreateDirectory(_temporaryDirectory);

    [Theory]
    [InlineData("service.ts")]
    [InlineData("component.tsx")]
    [InlineData("utils.js")]
    [InlineData("helpers.jsx")]
    [InlineData("module.mjs")]
    public void Then_CanParseReturnsTrueForTypeScriptAndJavaScriptFiles(string fileName) =>
        _sut.CanParse(fileName).ShouldBeTrue();

    [Theory]
    [InlineData("Program.cs")]
    [InlineData("main.py")]
    [InlineData("README.md")]
    public void Then_CanParseReturnsFalseForNonTypeScriptFiles(string fileName) =>
        _sut.CanParse(fileName).ShouldBeFalse();

    [Fact]
    public void Then_ExtractsClassDeclaration()
    {
        var sourceFilePath = WriteSourceFile("service.ts", """
            export class UserService {
            }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Class && n.Name == "UserService");
    }

    [Fact]
    public void Then_ExtractsAbstractClassDeclaration()
    {
        var sourceFilePath = WriteSourceFile("handler.ts", """
            export abstract class BaseHandler {
            }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Class && n.Name == "BaseHandler");
    }

    [Fact]
    public void Then_ExtractsInterfaceDeclaration()
    {
        var sourceFilePath = WriteSourceFile("service.ts", """
            export interface IUserService {
                getUser(): string;
            }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Interface && n.Name == "IUserService");
    }

    [Fact]
    public void Then_ExtractsFunctionDeclaration()
    {
        var sourceFilePath = WriteSourceFile("utils.ts", """
            export function calculateTotal(items: number[]): number {
                return items.reduce((a, b) => a + b, 0);
            }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Method && n.Name == "calculateTotal");
    }

    [Fact]
    public void Then_ExtractsMethodsInsideClass()
    {
        var sourceFilePath = WriteSourceFile("service.ts", """
            export class UserService {
                getUser(id: string): User {
                    return {} as User;
                }

                async saveUser(user: User): Promise<void> {
                    return;
                }
            }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Method && n.Name == "getUser");
        result.Nodes.ShouldContain(n => n.Kind == CodeNodeKind.Method && n.Name == "saveUser");
    }

    [Fact]
    public void Then_ExtractsContainsEdgeFromClassToMethod()
    {
        var sourceFilePath = WriteSourceFile("service.ts", """
            export class UserService {
                getUser(id: string): User {
                    return {} as User;
                }
            }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Edges.ShouldContain(e =>
            e.SourceIdentifier == "UserService" &&
            e.TargetIdentifier == "UserService.getUser" &&
            e.Kind == CodeEdgeKind.Contains);
    }

    [Fact]
    public void Then_ExtractsInheritsEdgeFromExtendsClause()
    {
        var sourceFilePath = WriteSourceFile("handler.ts", """
            export class AdminHandler extends BaseHandler {
            }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Edges.ShouldContain(e =>
            e.SourceIdentifier == "AdminHandler" &&
            e.TargetIdentifier == "BaseHandler" &&
            e.Kind == CodeEdgeKind.Inherits);
    }

    [Fact]
    public void Then_ExtractsImplementsEdgeFromImplementsClause()
    {
        var sourceFilePath = WriteSourceFile("service.ts", """
            export class UserService implements IUserService {
            }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Edges.ShouldContain(e =>
            e.SourceIdentifier == "UserService" &&
            e.TargetIdentifier == "IUserService" &&
            e.Kind == CodeEdgeKind.Implements);
    }

    [Fact]
    public void Then_ExtractsImportEdge()
    {
        var sourceFilePath = WriteSourceFile("service.ts", """
            import { Injectable } from '@angular/core';
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Edges.ShouldContain(e =>
            e.TargetIdentifier == "@angular/core" &&
            e.Kind == CodeEdgeKind.Imports);
    }

    [Fact]
    public void Then_DoesNotExtractControlFlowKeywordsAsMethods()
    {
        var sourceFilePath = WriteSourceFile("service.ts", """
            export class UserService {
                getUser(id: string): User {
                    if (id) {
                        return {} as User;
                    }
                    for (let i = 0; i < 10; i++) {}
                    while (true) {}
                    return {} as User;
                }
            }
            """);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldNotContain(n => n.Name == "if");
        result.Nodes.ShouldNotContain(n => n.Name == "for");
        result.Nodes.ShouldNotContain(n => n.Name == "while");
    }

    [Fact]
    public void Then_ReturnsEmptyResultForEmptyFile()
    {
        var sourceFilePath = WriteSourceFile("empty.ts", string.Empty);

        var result = _sut.Parse(sourceFilePath);

        result.Nodes.ShouldBeEmpty();
        result.Edges.ShouldBeEmpty();
    }

    public void Dispose() => Directory.Delete(_temporaryDirectory, recursive: true);

    private string WriteSourceFile(string fileName, string sourceCode)
    {
        var filePath = Path.Combine(_temporaryDirectory, fileName);
        File.WriteAllText(filePath, sourceCode);
        return filePath;
    }
}

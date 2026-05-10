using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KnowledgeSearch;

internal sealed class CodeStructureWalker : CSharpSyntaxWalker
{
    private readonly string _sourceFilePath;
    private readonly List<CodeNode> _nodes = [];
    private readonly List<CodeEdge> _edges = [];
    private string _currentNamespace = string.Empty;
    private string? _currentTypeIdentifier;

    internal CodeStructureWalker(string sourceFilePath) => _sourceFilePath = sourceFilePath;

    public IReadOnlyList<CodeNode> ExtractedNodes => _nodes;
    public IReadOnlyList<CodeEdge> ExtractedEdges => _edges;

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        _currentNamespace = node.Name.ToString();
        base.VisitFileScopedNamespaceDeclaration(node);
    }

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var previousNamespace = _currentNamespace;
        _currentNamespace = node.Name.ToString();
        base.VisitNamespaceDeclaration(node);
        _currentNamespace = previousNamespace;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var typeIdentifier = BuildTypeIdentifier(node.Identifier.Text);
        var declarationLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        _nodes.Add(new CodeNode(typeIdentifier, node.Identifier.Text, CodeNodeKind.Class, _sourceFilePath, declarationLine));

        if (node.BaseList is not null)
        {
            foreach (var baseType in node.BaseList.Types)
            {
                AddBaseTypeEdge(typeIdentifier, baseType.Type.ToString(), declarationLine);
            }
        }

        var previousTypeIdentifier = _currentTypeIdentifier;
        _currentTypeIdentifier = typeIdentifier;
        base.VisitClassDeclaration(node);
        _currentTypeIdentifier = previousTypeIdentifier;
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        var typeIdentifier = BuildTypeIdentifier(node.Identifier.Text);
        var declarationLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        _nodes.Add(new CodeNode(typeIdentifier, node.Identifier.Text, CodeNodeKind.Interface, _sourceFilePath, declarationLine));

        var previousTypeIdentifier = _currentTypeIdentifier;
        _currentTypeIdentifier = typeIdentifier;
        base.VisitInterfaceDeclaration(node);
        _currentTypeIdentifier = previousTypeIdentifier;
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        var typeIdentifier = BuildTypeIdentifier(node.Identifier.Text);
        var declarationLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        _nodes.Add(new CodeNode(typeIdentifier, node.Identifier.Text, CodeNodeKind.Record, _sourceFilePath, declarationLine));

        var previousTypeIdentifier = _currentTypeIdentifier;
        _currentTypeIdentifier = typeIdentifier;
        base.VisitRecordDeclaration(node);
        _currentTypeIdentifier = previousTypeIdentifier;
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        var typeIdentifier = BuildTypeIdentifier(node.Identifier.Text);
        var declarationLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        _nodes.Add(new CodeNode(typeIdentifier, node.Identifier.Text, CodeNodeKind.Enum, _sourceFilePath, declarationLine));
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (_currentTypeIdentifier is null)
        {
            return;
        }

        var methodIdentifier = $"{_currentTypeIdentifier}.{node.Identifier.Text}";
        var declarationLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        _nodes.Add(new CodeNode(methodIdentifier, node.Identifier.Text, CodeNodeKind.Method, _sourceFilePath, declarationLine));
        _edges.Add(new CodeEdge(_currentTypeIdentifier, methodIdentifier, CodeEdgeKind.Contains, declarationLine));

        base.VisitMethodDeclaration(node);
    }

    private string BuildTypeIdentifier(string typeName) =>
        string.IsNullOrEmpty(_currentNamespace) ? typeName : $"{_currentNamespace}.{typeName}";

    private void AddBaseTypeEdge(string typeIdentifier, string baseTypeName, int declarationLine)
    {
        var edgeKind = LooksLikeInterface(baseTypeName) ? CodeEdgeKind.Implements : CodeEdgeKind.Inherits;
        _edges.Add(new CodeEdge(typeIdentifier, baseTypeName, edgeKind, declarationLine));
    }

    private static bool LooksLikeInterface(string typeName)
    {
        var simpleName = typeName.Contains('<') ? typeName[..typeName.IndexOf('<')] : typeName;
        return simpleName.Length >= 2 && simpleName[0] == 'I' && char.IsUpper(simpleName[1]);
    }
}

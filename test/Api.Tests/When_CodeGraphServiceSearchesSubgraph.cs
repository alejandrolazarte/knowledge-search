using KnowledgeSearch;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_CodeGraphServiceSearchesSubgraph
{
    private readonly Mock<ICodeGraphRepository> _mockRepository = new();

    [Fact]
    public void Then_ReturnsMatchingNodesAtDepthZero()
    {
        var userServiceNode = new CodeNode("App.UserService", "UserService", CodeNodeKind.Class, "/src/UserService.cs", 1);
        var orderServiceNode = new CodeNode("App.OrderService", "OrderService", CodeNodeKind.Class, "/src/OrderService.cs", 1);

        _mockRepository.Setup(r => r.SearchNodes("my-repo", "UserService")).Returns([userServiceNode]);
        _mockRepository.Setup(r => r.GetNodes("my-repo")).Returns([userServiceNode, orderServiceNode]);
        _mockRepository.Setup(r => r.GetEdges("my-repo")).Returns([]);

        var result = BuildService().SearchSubgraph("my-repo", "UserService", depth: 0);

        result.Nodes.ShouldHaveSingleItem();
        result.Nodes[0].Name.ShouldBe("UserService");
        result.TotalFound.ShouldBe(1);
    }

    [Fact]
    public void Then_ExpandsToDirectNeighborsAtDepthOne()
    {
        var userServiceNode = new CodeNode("App.UserService", "UserService", CodeNodeKind.Class, "/src/UserService.cs", 1);
        var iUserServiceNode = new CodeNode("App.IUserService", "IUserService", CodeNodeKind.Interface, "/src/IUserService.cs", 1);
        var unrelatedNode = new CodeNode("App.OrderService", "OrderService", CodeNodeKind.Class, "/src/OrderService.cs", 1);
        var edge = new CodeEdge("App.UserService", "App.IUserService", CodeEdgeKind.Implements, 1);

        _mockRepository.Setup(r => r.SearchNodes("my-repo", "UserService")).Returns([userServiceNode]);
        _mockRepository.Setup(r => r.GetNodes("my-repo")).Returns([userServiceNode, iUserServiceNode, unrelatedNode]);
        _mockRepository.Setup(r => r.GetEdges("my-repo")).Returns([edge]);

        var result = BuildService().SearchSubgraph("my-repo", "UserService", depth: 1);

        result.Nodes.Count.ShouldBe(2);
        result.Nodes.ShouldContain(n => n.Name == "UserService");
        result.Nodes.ShouldContain(n => n.Name == "IUserService");
        result.Edges.ShouldHaveSingleItem();
    }

    [Fact]
    public void Then_ReturnsEmptySubgraphWhenQueryMatchesNothing()
    {
        _mockRepository.Setup(r => r.SearchNodes("my-repo", "NonExistent")).Returns([]);
        _mockRepository.Setup(r => r.GetNodes("my-repo")).Returns([]);
        _mockRepository.Setup(r => r.GetEdges("my-repo")).Returns([]);

        var result = BuildService().SearchSubgraph("my-repo", "NonExistent", depth: 2);

        result.Nodes.ShouldBeEmpty();
        result.Edges.ShouldBeEmpty();
        result.TotalFound.ShouldBe(0);
    }

    [Fact]
    public void Then_NodeWeightReflectsNumberOfConnectingEdges()
    {
        var serviceNode = new CodeNode("App.Service", "Service", CodeNodeKind.Class, "/src/Service.cs", 1);
        var iService1Node = new CodeNode("App.IService1", "IService1", CodeNodeKind.Interface, "/src/IService1.cs", 1);
        var iService2Node = new CodeNode("App.IService2", "IService2", CodeNodeKind.Interface, "/src/IService2.cs", 1);
        var edges = new List<CodeEdge>
        {
            new("App.Service", "App.IService1", CodeEdgeKind.Implements, 1),
            new("App.Service", "App.IService2", CodeEdgeKind.Implements, 1),
        };

        _mockRepository.Setup(r => r.SearchNodes("my-repo", "Service")).Returns([serviceNode]);
        _mockRepository.Setup(r => r.GetNodes("my-repo")).Returns([serviceNode, iService1Node, iService2Node]);
        _mockRepository.Setup(r => r.GetEdges("my-repo")).Returns(edges);

        var result = BuildService().SearchSubgraph("my-repo", "Service", depth: 1);

        result.NodeWeights["App.Service"].ShouldBe(2);
    }

    [Fact]
    public void Then_DoesNotReturnDuplicateNodesForCircularEdges()
    {
        var nodeA = new CodeNode("App.A", "A", CodeNodeKind.Class, "/src/A.cs", 1);
        var nodeB = new CodeNode("App.B", "B", CodeNodeKind.Class, "/src/B.cs", 1);
        var edges = new List<CodeEdge>
        {
            new("App.A", "App.B", CodeEdgeKind.Contains, 1),
            new("App.B", "App.A", CodeEdgeKind.Inherits, 1),
        };

        _mockRepository.Setup(r => r.SearchNodes("my-repo", "A")).Returns([nodeA]);
        _mockRepository.Setup(r => r.GetNodes("my-repo")).Returns([nodeA, nodeB]);
        _mockRepository.Setup(r => r.GetEdges("my-repo")).Returns(edges);

        var result = BuildService().SearchSubgraph("my-repo", "A", depth: 2);

        result.Nodes.Count.ShouldBe(2);
        result.Nodes.Count(n => n.Identifier == "App.A").ShouldBe(1);
    }

    private CodeGraphService BuildService()
    {
        var services = new ServiceCollection();
        return new CodeGraphService(services.BuildServiceProvider(), _mockRepository.Object);
    }
}

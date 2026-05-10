using KnowledgeSearch;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_CodeGraphServiceSearchesAcrossRepositories
{
    private readonly Mock<ICodeGraphRepository> _mockRepository = new();

    [Fact]
    public void Then_ReturnsNodesFromAllRepositoriesThatMatchQuery()
    {
        var eventNode = new CodeNode("Users.UserCreatedIntegrationEvent", "UserCreatedIntegrationEvent", CodeNodeKind.Class, "/users/UserCreatedIntegrationEvent.cs", 1);
        var handlerNode = new CodeNode("Notifications.UserCreatedIntegrationEventHandler", "UserCreatedIntegrationEventHandler", CodeNodeKind.Class, "/notifications/UserCreatedIntegrationEventHandler.cs", 1);

        _mockRepository.Setup(r => r.GetRepositoryNames()).Returns(["users-ms", "notifications-ms"]);
        _mockRepository.Setup(r => r.SearchNodes("users-ms", "IntegrationEvent")).Returns([eventNode]);
        _mockRepository.Setup(r => r.GetNodes("users-ms")).Returns([eventNode]);
        _mockRepository.Setup(r => r.GetEdges("users-ms")).Returns([]);
        _mockRepository.Setup(r => r.SearchNodes("notifications-ms", "IntegrationEvent")).Returns([handlerNode]);
        _mockRepository.Setup(r => r.GetNodes("notifications-ms")).Returns([handlerNode]);
        _mockRepository.Setup(r => r.GetEdges("notifications-ms")).Returns([]);

        var result = BuildService().SearchSubgraphAcrossRepositories("IntegrationEvent", depth: 0);

        result.Nodes.Count.ShouldBe(2);
        result.Nodes.ShouldContain(n => n.RepositoryName == "users-ms" && n.Node.Name == "UserCreatedIntegrationEvent");
        result.Nodes.ShouldContain(n => n.RepositoryName == "notifications-ms" && n.Node.Name == "UserCreatedIntegrationEventHandler");
    }

    [Fact]
    public void Then_TotalFoundIsTheSumOfSeedNodesAcrossAllRepositories()
    {
        var eventNode = new CodeNode("Users.Event", "UserCreatedIntegrationEvent", CodeNodeKind.Class, "/users/Event.cs", 1);
        var handlerNode = new CodeNode("Notif.Handler", "UserCreatedIntegrationEventHandler", CodeNodeKind.Class, "/notif/Handler.cs", 1);

        _mockRepository.Setup(r => r.GetRepositoryNames()).Returns(["users-ms", "notifications-ms"]);
        _mockRepository.Setup(r => r.SearchNodes("users-ms", "IntegrationEvent")).Returns([eventNode]);
        _mockRepository.Setup(r => r.GetNodes("users-ms")).Returns([eventNode]);
        _mockRepository.Setup(r => r.GetEdges("users-ms")).Returns([]);
        _mockRepository.Setup(r => r.SearchNodes("notifications-ms", "IntegrationEvent")).Returns([handlerNode]);
        _mockRepository.Setup(r => r.GetNodes("notifications-ms")).Returns([handlerNode]);
        _mockRepository.Setup(r => r.GetEdges("notifications-ms")).Returns([]);

        var result = BuildService().SearchSubgraphAcrossRepositories("IntegrationEvent", depth: 0);

        result.TotalFound.ShouldBe(2);
    }

    [Fact]
    public void Then_SkipsRepositoriesWithNoMatchingNodes()
    {
        var handlerNode = new CodeNode("Notif.Handler", "UserCreatedIntegrationEventHandler", CodeNodeKind.Class, "/notif/Handler.cs", 1);

        _mockRepository.Setup(r => r.GetRepositoryNames()).Returns(["users-ms", "notifications-ms"]);
        _mockRepository.Setup(r => r.SearchNodes("users-ms", "IntegrationEvent")).Returns([]);
        _mockRepository.Setup(r => r.SearchNodes("notifications-ms", "IntegrationEvent")).Returns([handlerNode]);
        _mockRepository.Setup(r => r.GetNodes("notifications-ms")).Returns([handlerNode]);
        _mockRepository.Setup(r => r.GetEdges("notifications-ms")).Returns([]);

        var result = BuildService().SearchSubgraphAcrossRepositories("IntegrationEvent", depth: 0);

        result.Nodes.ShouldHaveSingleItem();
        result.Nodes[0].RepositoryName.ShouldBe("notifications-ms");
    }

    [Fact]
    public void Then_IncludesEdgesTaggedWithTheirRepository()
    {
        var handlerNode = new CodeNode("Notif.Handler", "UserCreatedIntegrationEventHandler", CodeNodeKind.Class, "/notif/Handler.cs", 1);
        var interfaceNode = new CodeNode("Notif.IHandler", "IIntegrationEventHandler", CodeNodeKind.Interface, "/notif/IIntegrationEventHandler.cs", 1);
        var edge = new CodeEdge("Notif.Handler", "Notif.IHandler", CodeEdgeKind.Implements, 1);

        _mockRepository.Setup(r => r.GetRepositoryNames()).Returns(["notifications-ms"]);
        _mockRepository.Setup(r => r.SearchNodes("notifications-ms", "Handler")).Returns([handlerNode]);
        _mockRepository.Setup(r => r.GetNodes("notifications-ms")).Returns([handlerNode, interfaceNode]);
        _mockRepository.Setup(r => r.GetEdges("notifications-ms")).Returns([edge]);

        var result = BuildService().SearchSubgraphAcrossRepositories("Handler", depth: 1);

        result.Edges.ShouldHaveSingleItem();
        result.Edges[0].RepositoryName.ShouldBe("notifications-ms");
    }

    [Fact]
    public void Then_ReturnsEmptyResultWhenNoRepositoryHasMatchingNodes()
    {
        _mockRepository.Setup(r => r.GetRepositoryNames()).Returns(["users-ms", "orders-ms"]);
        _mockRepository.Setup(r => r.SearchNodes("users-ms", "NonExistent")).Returns([]);
        _mockRepository.Setup(r => r.SearchNodes("orders-ms", "NonExistent")).Returns([]);

        var result = BuildService().SearchSubgraphAcrossRepositories("NonExistent", depth: 1);

        result.Nodes.ShouldBeEmpty();
        result.Edges.ShouldBeEmpty();
        result.TotalFound.ShouldBe(0);
    }

    private CodeGraphService BuildService()
    {
        var services = new ServiceCollection();
        return new CodeGraphService(services.BuildServiceProvider(), _mockRepository.Object);
    }
}

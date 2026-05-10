using KnowledgeSearch;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_CodeGraphServiceBuildsCrossRepoEdges
{
    private readonly Mock<ICodeGraphRepository> _mockRepository = new();

    [Fact]
    public void Then_DetectsNameContainmentAcrossRepos()
    {
        var eventNode = new CodeNode("Users.UserCreatedIntegrationEvent", "UserCreatedIntegrationEvent", CodeNodeKind.Class, "/users/Event.cs", 1);
        var handlerNode = new CodeNode("Notif.UserCreatedIntegrationEventHandler", "UserCreatedIntegrationEventHandler", CodeNodeKind.Class, "/notif/Handler.cs", 1);

        _mockRepository.Setup(r => r.GetRepositoryNames()).Returns(["users-ms", "notifications-ms"]);
        _mockRepository.Setup(r => r.GetNodes("users-ms")).Returns([eventNode]);
        _mockRepository.Setup(r => r.GetEdges("users-ms")).Returns([]);
        _mockRepository.Setup(r => r.GetNodes("notifications-ms")).Returns([handlerNode]);
        _mockRepository.Setup(r => r.GetEdges("notifications-ms")).Returns([]);

        var result = BuildService().BuildCrossRepoEdges();

        result.ShouldHaveSingleItem();
        result[0].SourceRepositoryName.ShouldBe("notifications-ms");
        result[0].SourceIdentifier.ShouldBe("Notif.UserCreatedIntegrationEventHandler");
        result[0].TargetRepositoryName.ShouldBe("users-ms");
        result[0].TargetIdentifier.ShouldBe("Users.UserCreatedIntegrationEvent");
        result[0].Kind.ShouldBe(CrossRepoEdgeKind.References);
    }

    [Fact]
    public void Then_DetectsEdgeTargetContainmentAcrossRepos()
    {
        var eventNode = new CodeNode("Users.UserCreatedIntegrationEvent", "UserCreatedIntegrationEvent", CodeNodeKind.Class, "/users/Event.cs", 1);
        var handlerNode = new CodeNode("Notif.Handler", "Handler", CodeNodeKind.Class, "/notif/Handler.cs", 1);
        var implementsEdge = new CodeEdge("Notif.Handler", "IIntegrationEventHandler<UserCreatedIntegrationEvent>", CodeEdgeKind.Implements, 1);

        _mockRepository.Setup(r => r.GetRepositoryNames()).Returns(["users-ms", "notifications-ms"]);
        _mockRepository.Setup(r => r.GetNodes("users-ms")).Returns([eventNode]);
        _mockRepository.Setup(r => r.GetEdges("users-ms")).Returns([]);
        _mockRepository.Setup(r => r.GetNodes("notifications-ms")).Returns([handlerNode]);
        _mockRepository.Setup(r => r.GetEdges("notifications-ms")).Returns([implementsEdge]);

        var result = BuildService().BuildCrossRepoEdges();

        result.ShouldHaveSingleItem();
        result[0].SourceIdentifier.ShouldBe("Notif.Handler");
        result[0].TargetIdentifier.ShouldBe("Users.UserCreatedIntegrationEvent");
    }

    [Fact]
    public void Then_SkipsNodeNamesWithLessThanEightCharacters()
    {
        var shortNameNode = new CodeNode("A.MyClass", "MyClass", CodeNodeKind.Class, "/a/MyClass.cs", 1);
        var referencingNode = new CodeNode("B.MyClassHandler", "MyClassHandler", CodeNodeKind.Class, "/b/MyClassHandler.cs", 1);

        _mockRepository.Setup(r => r.GetRepositoryNames()).Returns(["repo-a", "repo-b"]);
        _mockRepository.Setup(r => r.GetNodes("repo-a")).Returns([shortNameNode]);
        _mockRepository.Setup(r => r.GetEdges("repo-a")).Returns([]);
        _mockRepository.Setup(r => r.GetNodes("repo-b")).Returns([referencingNode]);
        _mockRepository.Setup(r => r.GetEdges("repo-b")).Returns([]);

        var result = BuildService().BuildCrossRepoEdges();

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Then_DoesNotCreateEdgesWithinTheSameRepository()
    {
        var eventNode = new CodeNode("App.UserCreatedIntegrationEvent", "UserCreatedIntegrationEvent", CodeNodeKind.Class, "/Event.cs", 1);
        var handlerNode = new CodeNode("App.UserCreatedIntegrationEventHandler", "UserCreatedIntegrationEventHandler", CodeNodeKind.Class, "/Handler.cs", 1);

        _mockRepository.Setup(r => r.GetRepositoryNames()).Returns(["single-repo"]);
        _mockRepository.Setup(r => r.GetNodes("single-repo")).Returns([eventNode, handlerNode]);
        _mockRepository.Setup(r => r.GetEdges("single-repo")).Returns([]);

        var result = BuildService().BuildCrossRepoEdges();

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Then_DoesNotCreateDuplicateEdgesWhenBothNameAndTargetMatch()
    {
        var eventNode = new CodeNode("Users.UserCreatedIntegrationEvent", "UserCreatedIntegrationEvent", CodeNodeKind.Class, "/Event.cs", 1);
        var handlerNode = new CodeNode("Notif.UserCreatedIntegrationEventHandler", "UserCreatedIntegrationEventHandler", CodeNodeKind.Class, "/Handler.cs", 1);
        var implementsEdge = new CodeEdge("Notif.UserCreatedIntegrationEventHandler", "IHandler<UserCreatedIntegrationEvent>", CodeEdgeKind.Implements, 1);

        _mockRepository.Setup(r => r.GetRepositoryNames()).Returns(["users-ms", "notifications-ms"]);
        _mockRepository.Setup(r => r.GetNodes("users-ms")).Returns([eventNode]);
        _mockRepository.Setup(r => r.GetEdges("users-ms")).Returns([]);
        _mockRepository.Setup(r => r.GetNodes("notifications-ms")).Returns([handlerNode]);
        _mockRepository.Setup(r => r.GetEdges("notifications-ms")).Returns([implementsEdge]);

        var result = BuildService().BuildCrossRepoEdges();

        result.Count(e => e.SourceIdentifier == "Notif.UserCreatedIntegrationEventHandler"
                       && e.TargetIdentifier == "Users.UserCreatedIntegrationEvent").ShouldBe(1);
    }

    [Fact]
    public void Then_ReturnsEmptyWhenOnlyOneRepositoryExists()
    {
        var eventNode = new CodeNode("App.SomeClass", "SomeClass", CodeNodeKind.Class, "/SomeClass.cs", 1);

        _mockRepository.Setup(r => r.GetRepositoryNames()).Returns(["single-repo"]);
        _mockRepository.Setup(r => r.GetNodes("single-repo")).Returns([eventNode]);
        _mockRepository.Setup(r => r.GetEdges("single-repo")).Returns([]);

        var result = BuildService().BuildCrossRepoEdges();

        result.ShouldBeEmpty();
    }

    private CodeGraphService BuildService()
    {
        var services = new ServiceCollection();
        return new CodeGraphService(services.BuildServiceProvider(), _mockRepository.Object);
    }
}

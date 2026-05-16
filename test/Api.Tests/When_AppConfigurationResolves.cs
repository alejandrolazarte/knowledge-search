using KnowledgeSearch;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_AppConfigurationResolves
{
    [Fact]
    public void Then_KnowledgeDirsEnvironmentOverridesConfiguredKnowledgeDirs()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KnowledgeDirs"] = "D:/Documentation",
            })
            .Build();
        var environment = new Dictionary<string, string?>
        {
            ["KNOWLEDGE_DIRS"] = "/data/knowledge",
        };

        var roots = AppConfiguration.ResolveKnowledgeRoots(configuration, environment.GetValueOrDefault);

        roots.ShouldBe([Path.GetFullPath("/data/knowledge")]);
    }
}

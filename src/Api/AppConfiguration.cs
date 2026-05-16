using Microsoft.Extensions.Configuration;

namespace KnowledgeSearch;

internal static class AppConfiguration
{
    public static string[] ResolveKnowledgeRoots(IConfiguration configuration, Func<string, string?> getEnvironmentVariable)
    {
        var configuredRoots = getEnvironmentVariable("KNOWLEDGE_DIRS")
            ?? configuration["KnowledgeDirs"]
            ?? Path.GetFullPath("../knowledge");

        return configuredRoots
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(Path.GetFullPath)
            .ToArray();
    }
}

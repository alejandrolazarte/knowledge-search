using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using KnowledgeSearch.Core.Domain.Sources;

namespace KnowledgeSearch;

internal static class AppConfiguration
{
    private static readonly JsonSerializerOptions _sourceConfigurationJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    static AppConfiguration()
    {
        _sourceConfigurationJsonOptions.Converters.Add(new JsonStringEnumConverter<SourceKind>());
    }

    public static ResolvedSourceConfiguration ResolveSources(
        IConfiguration configuration,
        Func<string, string?> getEnvironmentVariable)
    {
        var sourcesConfigPath = getEnvironmentVariable("SOURCES_CONFIG")
            ?? configuration["SourcesConfig"]
            ?? Path.Combine(AppContext.BaseDirectory, "data", "sources.json");

        if (File.Exists(sourcesConfigPath))
        {
            var json = File.ReadAllText(sourcesConfigPath);
            var sourceConfiguration = JsonSerializer.Deserialize<SourceConfigurationFile>(
                json,
                _sourceConfigurationJsonOptions);

            var configuredSources = sourceConfiguration?.Sources
                .Select(source => source.ToConfiguredSource())
                .ToList()
                ?? [];

            return new ResolvedSourceConfiguration(
                configuredSources,
                configuredSources
                    .Where(source => source.IndexDocs)
                    .Select(source => source.GetAccessiblePath())
                    .ToArray());
        }

        var fallbackRoots = ResolveKnowledgeRoots(configuration, getEnvironmentVariable);
        var fallbackSources = fallbackRoots
            .Select(root => ConfiguredSource.Create(
                ConfiguredSource.CreateId(root),
                Path.GetFileName(Path.TrimEndingDirectorySeparator(root)),
                SourceKind.Knowledge,
                root,
                indexCode: false,
                indexDocs: true))
            .ToArray();

        return new ResolvedSourceConfiguration(fallbackSources, fallbackRoots);
    }

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

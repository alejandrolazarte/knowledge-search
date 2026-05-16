using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace KnowledgeSearch;

internal sealed class SourceConfigurationService(
    IConfiguration configuration,
    Func<string, string?> getEnvironmentVariable) : ISourceConfigurationService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    static SourceConfigurationService()
    {
        _jsonOptions.Converters.Add(new JsonStringEnumConverter<SourceKind>());
    }

    private readonly object _lock = new();

    public SourceConfigurationFile GetConfiguration()
    {
        lock (_lock)
        {
            return ReadConfiguration();
        }
    }

    public IReadOnlyList<string> GetKnowledgeRootNames()
    {
        return GetConfiguration()
            .Sources
            .Select(source => source.ToConfiguredSource())
            .Where(source => source.IndexDocs)
            .Select(source => Path.GetFileName(
                Path.TrimEndingDirectorySeparator(
                    ConfiguredSource.ToAccessiblePath(source.HostPath))))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
    }

    public string ExportJson()
    {
        return JsonSerializer.Serialize(GetConfiguration(), _jsonOptions);
    }

    public SaveSourcesResult Save(SourceConfigurationFile configuration)
    {
        var normalized = Normalize(configuration);
        var validationError = Validate(normalized);
        if (validationError is not null)
        {
            return new SaveSourcesResult(false, validationError);
        }

        lock (_lock)
        {
            var path = GetSourcesConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(normalized, _jsonOptions));
        }

        return new SaveSourcesResult(true, null);
    }

    private SourceConfigurationFile ReadConfiguration()
    {
        var path = GetSourcesConfigPath();
        if (File.Exists(path))
        {
            var fromFile = JsonSerializer.Deserialize<SourceConfigurationFile>(
                File.ReadAllText(path),
                _jsonOptions);
            return Normalize(fromFile ?? new SourceConfigurationFile(1, []));
        }

        return new SourceConfigurationFile(1, []);
    }

    private static SourceConfigurationFile Normalize(SourceConfigurationFile configuration)
    {
        var sources = configuration.Sources
            .Select(source => SourceDefinition.FromConfiguredSource(source.ToConfiguredSource()))
            .ToArray();
        return new SourceConfigurationFile(configuration.Version <= 0 ? 1 : configuration.Version, sources);
    }

    private static string? Validate(SourceConfigurationFile configuration)
    {
        if (configuration.Sources is null)
        {
            return "sources no puede ser null.";
        }

        foreach (var source in configuration.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.Id)
                || string.IsNullOrWhiteSpace(source.Name)
                || string.IsNullOrWhiteSpace(source.HostPath))
            {
                return "Cada source requiere id, name y hostPath.";
            }
        }

        var duplicateId = configuration.Sources
            .GroupBy(source => source.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        return duplicateId is null ? null : $"id duplicado: {duplicateId}";
    }

    private string GetSourcesConfigPath()
    {
        return getEnvironmentVariable("SOURCES_CONFIG")
            ?? configuration["SourcesConfig"]
            ?? Path.GetFullPath("../data/sources.json");
    }
}

namespace KnowledgeSearch.Core.Domain.Sources;

public sealed record SourceConfigurationFile(
    int Version,
    IReadOnlyList<SourceDefinition> Sources);

public sealed record SourceDefinition(
    string Id,
    string Name,
    SourceKind Kind,
    string HostPath,
    bool? IndexCode,
    bool? IndexDocs,
    IReadOnlyList<string>? DocIncludes,
    IReadOnlyList<string>? CodeIncludes,
    IReadOnlyList<string>? Excludes)
{
    public ConfiguredSource ToConfiguredSource()
    {
        return ConfiguredSource.Create(
            string.IsNullOrWhiteSpace(Id) ? ConfiguredSource.CreateId(HostPath) : Id,
            string.IsNullOrWhiteSpace(Name) ? ConfiguredSource.CreateId(HostPath) : Name,
            Kind,
            HostPath,
            IndexCode,
            IndexDocs,
            DocIncludes is { Count: > 0 } ? DocIncludes : null,
            CodeIncludes is { Count: > 0 } ? CodeIncludes : null,
            Excludes is { Count: > 0 } ? Excludes : null);
    }

    public static SourceDefinition FromConfiguredSource(ConfiguredSource source)
    {
        return new SourceDefinition(
            source.Id,
            source.Name,
            source.Kind,
            source.HostPath,
            source.IndexCode,
            source.IndexDocs,
            source.DocIncludes,
            source.CodeIncludes,
            source.Excludes);
    }
}

public sealed record ResolvedSourceConfiguration(
    IReadOnlyList<ConfiguredSource> Sources,
    IReadOnlyList<string> KnowledgeRoots);

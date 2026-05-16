using System.Text.Json.Serialization;

namespace KnowledgeSearch;

[JsonConverter(typeof(JsonStringEnumConverter<SourceKind>))]
internal enum SourceKind
{
    Knowledge,
    Repository,
}

internal sealed record SourceConfigurationFile(
    int Version,
    IReadOnlyList<SourceDefinition> Sources);

internal sealed record SourceDefinition(
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
            string.IsNullOrWhiteSpace(Name) ? Path.GetFileName(Path.TrimEndingDirectorySeparator(HostPath)) : Name,
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

internal sealed record ConfiguredSource(
    string Id,
    string Name,
    SourceKind Kind,
    string HostPath,
    bool IndexCode,
    bool IndexDocs,
    IReadOnlyList<string> DocIncludes,
    IReadOnlyList<string> CodeIncludes,
    IReadOnlyList<string> Excludes)
{
    public static ConfiguredSource Create(
        string id,
        string name,
        SourceKind kind,
        string hostPath,
        bool? indexCode = null,
        bool? indexDocs = null,
        IReadOnlyList<string>? docIncludes = null,
        IReadOnlyList<string>? codeIncludes = null,
        IReadOnlyList<string>? excludes = null)
    {
        return new ConfiguredSource(
            id,
            name,
            kind,
            Path.GetFullPath(hostPath),
            indexCode ?? kind == SourceKind.Repository,
            indexDocs ?? true,
            docIncludes ?? GetDefaultDocIncludes(kind),
            codeIncludes ?? DefaultCodeIncludes,
            excludes ?? DefaultExcludes);
    }

    public static readonly IReadOnlyList<string> DefaultExcludes =
    [
        "**/.git/**",
        "**/node_modules/**",
        "**/bin/**",
        "**/obj/**",
        "**/dist/**",
        "**/build/**",
        "**/.next/**",
        "**/coverage/**",
    ];

    public static readonly IReadOnlyList<string> DefaultRepositoryDocIncludes =
    [
        "README.md",
        "docs/**/*.md",
        "specs/**/*.md",
        "adr/**/*.md",
    ];

    public static readonly IReadOnlyList<string> DefaultKnowledgeDocIncludes =
    [
        "**/*.md",
        "**/*.mdx",
    ];

    public static readonly IReadOnlyList<string> DefaultCodeIncludes =
    [
        "**/*.cs",
        "**/*.ts",
        "**/*.tsx",
        "**/*.js",
        "**/*.jsx",
        "**/*.py",
    ];

    internal static string CreateId(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return string.IsNullOrWhiteSpace(name) ? "source" : name;
    }

    private static IReadOnlyList<string> GetDefaultDocIncludes(SourceKind kind) =>
        kind == SourceKind.Repository ? DefaultRepositoryDocIncludes : DefaultKnowledgeDocIncludes;
}

internal sealed record ResolvedSourceConfiguration(
    IReadOnlyList<ConfiguredSource> Sources,
    IReadOnlyList<string> KnowledgeRoots);

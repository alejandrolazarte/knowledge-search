using System.Runtime.InteropServices;

namespace KnowledgeSearch.Core.Domain.Sources;

public sealed record ConfiguredSource(
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
            NormalizeHostPath(hostPath),
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

    public static string CreateId(string path)
    {
        var name = GetLastPathSegment(path);
        return string.IsNullOrWhiteSpace(name) ? "source" : name;
    }

    public static string ToAccessiblePath(string hostPath)
    {
        return ToAccessiblePath(CreateId(hostPath), hostPath);
    }

    public static string ToAccessiblePath(string id, string hostPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && hostPath.Length >= 2
            && char.IsLetter(hostPath[0])
            && hostPath[1] == ':')
        {
            return GetContainerSourcePath(id);
        }

        return hostPath;
    }

    public string GetAccessiblePath() => ToAccessiblePath(Id, HostPath);

    public static string GetContainerSourcePath(string id) =>
        $"/data/sources/{SanitizeContainerPathSegment(id)}";

    private static string SanitizeContainerPathSegment(string value)
    {
        var chars = value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-')
            .ToArray();
        var sanitized = new string(chars).Trim('-', '.', '_');
        return string.IsNullOrWhiteSpace(sanitized) ? "source" : sanitized;
    }

    private static string NormalizeHostPath(string path)
    {
        if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
        {
            return path;
        }

        return Path.GetFullPath(path);
    }

    private static IReadOnlyList<string> GetDefaultDocIncludes(SourceKind kind) =>
        kind == SourceKind.Repository ? DefaultRepositoryDocIncludes : DefaultKnowledgeDocIncludes;

    private static string GetLastPathSegment(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\', '/');
        var lastSeparator = trimmed.LastIndexOfAny(['\\', '/']);
        return lastSeparator >= 0 ? trimmed[(lastSeparator + 1)..] : Path.GetFileName(trimmed);
    }
}

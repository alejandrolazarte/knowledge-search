using System.Text.Json.Serialization;

namespace KnowledgeSearch;

internal record SearchResult(string Title, string Section, string Path, int Line, string Content);
internal record IndexResult(int Added, int Updated, int Deleted);
internal record HealthResult(string Status);
internal record SkillSummary(string Name, string Description, string DirName);
public record LogEvent(
    [property: JsonPropertyName("ts")]   string Ts,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("path")] string Path);

[JsonSerializable(typeof(List<SearchResult>))]
[JsonSerializable(typeof(List<SkillSummary>))]
[JsonSerializable(typeof(List<LogEvent>))]
[JsonSerializable(typeof(LogEvent))]
[JsonSerializable(typeof(IndexResult))]
[JsonSerializable(typeof(HealthResult))]
[JsonSerializable(typeof(string))]
internal partial class AppJsonContext : JsonSerializerContext { }

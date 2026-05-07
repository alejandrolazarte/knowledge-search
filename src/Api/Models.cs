using System.Text.Json.Serialization;

namespace KnowledgeSearch;

record SearchResult(string Title, string Section, string Path, int Line, string Content);
record IndexResult(int Added, int Updated, int Deleted);
record HealthResult(string Status);
record SkillSummary(string Name, string Description, string DirName);

[JsonSerializable(typeof(List<SearchResult>))]
[JsonSerializable(typeof(List<SkillSummary>))]
[JsonSerializable(typeof(IndexResult))]
[JsonSerializable(typeof(HealthResult))]
[JsonSerializable(typeof(string))]
internal partial class AppJsonContext : JsonSerializerContext { }

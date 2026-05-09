using System.Text.Json;

namespace KnowledgeSearch;

internal static class AppConfig
{
    public static (string DbPath, string DocsDir, string SkillsDir, string IndexHtmlPath, string StaticDir) Load()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(settingsPath))
        {
            settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        }

        JsonElement? settings = File.Exists(settingsPath)
            ? JsonDocument.Parse(File.ReadAllText(settingsPath)).RootElement
            : null;

        string Cfg(string key, string fallback) =>
            settings?.TryGetProperty(key, out var v) == true ? v.GetString()! : fallback;

        var dbPath    = Environment.GetEnvironmentVariable("KNOWLEDGE_DB")
                     ?? Cfg("KnowledgeDb",  Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "knowledge.db")));
        var docsDir   = Environment.GetEnvironmentVariable("KNOWLEDGE_DIR")
                     ?? Cfg("KnowledgeDir", Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "knowledge")));
        var skillsDir = Environment.GetEnvironmentVariable("SKILLS_DIR")
                     ?? Cfg("SkillsDir",    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills"));

        var indexHtmlPath = Path.Combine(AppContext.BaseDirectory, "index.html");
        var staticDir     = Path.GetDirectoryName(indexHtmlPath)!;

        return (dbPath, docsDir, skillsDir, indexHtmlPath, staticDir);
    }
}

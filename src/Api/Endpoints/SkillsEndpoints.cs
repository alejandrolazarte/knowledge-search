namespace KnowledgeSearch;

internal static class SkillsEndpoints
{
    public static void MapSkillsRoutes(this WebApplication app, string skillsDir)
    {
        app.MapGet("/skills/debug", () =>
        {
            var lines = new List<string> { "skillsDir: " + skillsDir, "exists: " + Directory.Exists(skillsDir), "" };
            try
            {
                foreach (var dir in Directory.GetDirectories(skillsDir).OrderBy(Path.GetFileName))
                {
                    var file   = Path.Combine(dir, "SKILL.md");
                    var exists = File.Exists(file);
                    string detail = exists ? "OK" : "NO SKILL.md";
                    if (exists)
                    {
                        try
                        {
                            File.ReadAllText(file);
                        }
                        catch (Exception ex)
                        {
                            detail = "READ ERR: " + ex.Message;
                        }
                    }
                    lines.Add(Path.GetFileName(dir) + " → " + detail);
                }
            }
            catch (Exception ex)
            {
                lines.Add("ERROR: " + ex.Message);
            }
            return Results.Text(string.Join("\n", lines), "text/plain");
        });

        app.MapGet("/skills", () =>
        {
            if (!Directory.Exists(skillsDir))
            {
                return Results.Ok(new List<SkillSummary>());
            }

            var skills = new List<SkillSummary>();
            foreach (var dir in Directory.GetDirectories(skillsDir).OrderBy(Path.GetFileName))
            {
                try
                {
                    var file = Path.Combine(dir, "SKILL.md");
                    if (!File.Exists(file))
                    {
                        continue;
                    }
                    var content      = File.ReadAllText(file);
                    var (name, desc) = ParseFrontmatter(content);
                    if (string.IsNullOrEmpty(name))
                    {
                        name = Path.GetFileName(dir);
                    }
                    skills.Add(new SkillSummary(name, desc, Path.GetFileName(dir)));
                }
                catch (Exception)
                {
                    // Best-effort: skip directories with broken symlinks or permission errors.
                }
            }
            return Results.Ok(skills);
        });

        app.MapGet("/skills/{dirName}", (string dirName) =>
        {
            if (string.IsNullOrEmpty(dirName) || dirName.Contains('/') || dirName.Contains('\\') || dirName.Contains(".."))
            {
                return Results.BadRequest("Nombre inválido");
            }

            var file = Path.Combine(skillsDir, dirName, "SKILL.md");
            if (!File.Exists(file))
            {
                return Results.NotFound();
            }
            return Results.Text(StripFrontmatter(File.ReadAllText(file)), "text/plain; charset=utf-8");
        });
    }

    static string StripFrontmatter(string content)
    {
        if (!content.StartsWith("---"))
        {
            return content;
        }
        var end = content.IndexOf("\n---", 3);
        return end == -1 ? content : content[(end + 4)..].TrimStart();
    }

    static (string name, string description) ParseFrontmatter(string content)
    {
        string name = "", description = "";
        bool inFm = false;
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line == "---")
            {
                if (!inFm)
                {
                    inFm = true;
                    continue;
                }
                else
                {
                    break;
                }
            }
            if (!inFm)
            {
                break;
            }
            if (line.StartsWith("name:"))
            {
                name = line[5..].Trim();
            }
            if (line.StartsWith("description:"))
            {
                description = line[12..].Trim();
            }
        }
        return (name, description);
    }
}

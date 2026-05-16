// Levanta knowledge-search (producción) en Podman.
//
// Uso (desde cualquier directorio dentro del repo):
//   dotnet run scripts/podman/podman-run.cs [-- --build] [-- --detach]
//
// No requiere variables de entorno. Las fuentes se configuran desde la UI de Sources.
// Variables de entorno opcionales:
//   SKILLS_DIR  path a los skills de Claude (default: ~/.claude/skills)

using System.Diagnostics;
using System.Text.Json;

var build  = args.Contains("--build");
var detach = args.Contains("--detach");

var root      = FindRoot();
var dataDir   = Path.Combine(root, "data");
var sourceMounts = BuildSourceMounts(dataDir);
var skills    = Environment.GetEnvironmentVariable("SKILLS_DIR")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills");

const string Image     = "localhost/knowledge-search";
const string Container = "knowledge-search";

if (build)
{
    Info("Buildando imagen...");
    Exec(["podman", "build", "-t", Image, "-f", Path.Combine(root, "Containerfile"), root]);
}

Silent(["podman", "rm", "-f", Container]);

string[] modeFlags = detach ? ["-d"] : ["-it", "--rm"];
string[] runArgs =
[
    "podman", "run",
    "--name", Container,
    "-p", "5111:5111",
    "-v", $"{dataDir}:/data/db:Z",
    ..sourceMounts,
    "-v", $"{skills}:/data/skills:Z",
    "-e", "KNOWLEDGE_DB=/data/db/knowledge.db",
    "-e", "SOURCES_CONFIG=/data/db/sources.json",
    "-e", "SKILLS_DIR=/data/skills",
    ..modeFlags,
    Image,
];

Info($"Iniciando {Container}...");
Exec(runArgs);

// ── helpers ──────────────────────────────────────────────────────────────────

static string FindRoot()
{
    for (var d = Directory.GetCurrentDirectory(); d is not null; d = Path.GetDirectoryName(d))
        if (File.Exists(Path.Combine(d, "Containerfile"))) return d;
    throw new InvalidOperationException("Project root no encontrado (buscando Containerfile).");
}

static string[] BuildSourceMounts(string dataDir)
{
    var sourcesJson = Path.Combine(dataDir, "sources.json");
    if (!File.Exists(sourcesJson))
    {
        return [];
    }

    var volumeArgs = new List<string>();
    var mountedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    using var document = JsonDocument.Parse(File.ReadAllText(sourcesJson));
    if (!TryGetProperty(document.RootElement, "sources", out var sourcesElement)
        || sourcesElement.ValueKind != JsonValueKind.Array)
    {
        return [];
    }

    foreach (var source in sourcesElement.EnumerateArray())
    {
        var hostPathValue = TryGetStringProperty(source, "hostPath");
        if (string.IsNullOrWhiteSpace(hostPathValue))
        {
            continue;
        }

        var hostPath = Path.GetFullPath(hostPathValue);
        if (!Directory.Exists(hostPath))
        {
            Info($"Source no encontrada, no se monta: {hostPath}");
            continue;
        }

        var idValue = TryGetStringProperty(source, "id");
        var id = string.IsNullOrWhiteSpace(idValue)
            ? Path.GetFileName(Path.TrimEndingDirectorySeparator(hostPath))
            : idValue;
        var containerPath = $"/data/sources/{SanitizeContainerPathSegment(id)}";
        if (!mountedDestinations.Add(containerPath))
        {
            throw new InvalidOperationException($"Source id duplicado para mount: {id}");
        }

        volumeArgs.Add("-v");
        volumeArgs.Add($"{hostPath}:{containerPath}:Z");
    }

    return volumeArgs.ToArray();
}

static string? TryGetStringProperty(JsonElement element, string propertyName)
{
    return TryGetProperty(element, propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
        ? property.GetString()
        : null;
}

static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
{
    foreach (var candidate in element.EnumerateObject())
    {
        if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
        {
            property = candidate.Value;
            return true;
        }
    }

    property = default;
    return false;
}

static string SanitizeContainerPathSegment(string value)
{
    var chars = value
        .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-')
        .ToArray();
    var sanitized = new string(chars).Trim('-', '.', '_');
    return string.IsNullOrWhiteSpace(sanitized) ? "source" : sanitized;
}

static void Info(string msg)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(msg);
    Console.ResetColor();
}

static void Exec(string[] args)
{
    var psi = new ProcessStartInfo(args[0]) { UseShellExecute = false };
    for (var i = 1; i < args.Length; i++) psi.ArgumentList.Add(args[i]);
    using var p = Process.Start(psi)!;
    p.WaitForExit();
    if (p.ExitCode != 0) Environment.Exit(p.ExitCode);
}

static void Silent(string[] args)
{
    var psi = new ProcessStartInfo(args[0])
    {
        UseShellExecute        = false,
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
    };
    for (var i = 1; i < args.Length; i++) psi.ArgumentList.Add(args[i]);
    using var p = Process.Start(psi)!;
    p.WaitForExit();
}

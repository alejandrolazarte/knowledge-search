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
using System.Text.RegularExpressions;

var build  = args.Contains("--build");
var detach = args.Contains("--detach");

var root      = FindRoot();
var dataDir   = Path.Combine(root, "data");
var driveMounts = BuildDriveMounts(dataDir);
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
    ..driveMounts,
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

static string[] BuildDriveMounts(string dataDir)
{
    var sourcesJson = Path.Combine(dataDir, "sources.json");
    if (!File.Exists(sourcesJson))
    {
        return [];
    }

    var json = File.ReadAllText(sourcesJson);
    var driveLetters = Regex.Matches(json, @"""hostPath""\s*:\s*""([A-Za-z]):\\")
        .Select(m => char.ToLowerInvariant(m.Groups[1].Value[0]))
        .Distinct()
        .ToArray();

    var volumeArgs = new List<string>();
    foreach (var drive in driveLetters)
    {
        var hostDrive = $"{char.ToUpperInvariant(drive)}:\\";
        volumeArgs.Add("-v");
        volumeArgs.Add($"{hostDrive}:/mnt/{drive}:Z");
    }

    return volumeArgs.ToArray();
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

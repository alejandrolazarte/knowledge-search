// Levanta knowledge-search (producción) en Podman.
//
// Uso (desde cualquier directorio dentro del repo):
//   dotnet run scripts/podman/podman-run.cs [-- --build] [-- --detach]
//
// Variables de entorno requeridas:
//   KNOWLEDGE_DIRS  path(s) a carpetas con archivos .md de knowledge, separados por ;
//
// Variables de entorno opcionales:
//   SKILLS_DIR      path a los skills de Claude (default: ~/.claude/skills)

using System.Diagnostics;

var build  = args.Contains("--build");
var detach = args.Contains("--detach");

var root      = FindRoot();
var dataDir   = Path.Combine(root, "data");
var knowledgeRoots = RequireEnv("KNOWLEDGE_DIRS",
    "Ejemplo: $env:KNOWLEDGE_DIRS = 'D:\\mis-docs\\knowledge'");
var knowledgeMounts = BuildKnowledgeMounts(knowledgeRoots);
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
    ..knowledgeMounts.VolumeArgs,
    "-v", $"{skills}:/data/skills:Z",
    "-e", "KNOWLEDGE_DB=/data/db/knowledge.db",
    "-e", $"KNOWLEDGE_DIRS={knowledgeMounts.ContainerRoots}",
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

static string RequireEnv(string name, string hint)
{
    var val = Environment.GetEnvironmentVariable(name);
    if (val is not null) return val;

    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"ERROR: {name} no está seteado. {hint}");
    Console.ResetColor();
    Environment.Exit(1);
    return null!;
}

static KnowledgeMounts BuildKnowledgeMounts(string configuredRoots)
{
    var hostRoots = configuredRoots
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(Path.GetFullPath)
        .ToArray();

    if (hostRoots.Length == 0)
    {
        throw new InvalidOperationException("KNOWLEDGE_DIRS no contiene roots válidos.");
    }

    var volumeArgs = new List<string>();
    var containerRoots = new List<string>();

    for (var i = 0; i < hostRoots.Length; i++)
    {
        var containerRoot = hostRoots.Length == 1
            ? "/data/knowledge"
            : $"/data/knowledge/root{i + 1}";

        volumeArgs.Add("-v");
        volumeArgs.Add($"{hostRoots[i]}:{containerRoot}:Z");
        containerRoots.Add(containerRoot);
    }

    return new KnowledgeMounts(volumeArgs.ToArray(), string.Join(';', containerRoots));
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

internal sealed record KnowledgeMounts(string[] VolumeArgs, string ContainerRoots);

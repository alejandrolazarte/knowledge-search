// Levanta knowledge-search en modo desarrollo dentro de Podman.
// Source code editado en el host, compilado y ejecutado en el contenedor.
//
// Uso (desde cualquier directorio dentro del repo):
//   dotnet run scripts/podman/podman-dev.cs [-- <flags>]
//
//   --build            construye la imagen dev (primera vez o tras cambiar Containerfile.dev)
//   --install-deps     pnpm install dentro del contenedor (primera vez o tras cambiar package.json)
//   --build-frontend   pnpm run build dentro del contenedor (tras cambiar app/)
//   (sin flags)        arranca dotnet watch con hot reload de .cs
//
// Variables de entorno requeridas:
//   KNOWLEDGE_DIR   path a la carpeta con los archivos .md de knowledge
//
// Variables de entorno opcionales:
//   SKILLS_DIR      path a los skills de Claude (default: ~/.claude/skills)

using System.Diagnostics;

var build         = args.Contains("--build");
var installDeps   = args.Contains("--install-deps");
var buildFrontend = args.Contains("--build-frontend");

var root      = FindRoot();
var dataDir   = Path.Combine(root, "data");
var knowledge = RequireEnv("KNOWLEDGE_DIR",
    "Ejemplo: $env:KNOWLEDGE_DIR = 'D:\\mis-docs\\knowledge'");
var skills    = Environment.GetEnvironmentVariable("SKILLS_DIR")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills");

const string Image      = "localhost/knowledge-search-dev";
const string Container  = "knowledge-search-dev";
const string NodeVolume = "knowledge-search-node_modules";

string[] baseVols =
[
    "-v", $"{root}:/workspace:Z",
    "-v", $"{NodeVolume}:/workspace/app/node_modules",
    "-v", $"{dataDir}:/data/db:Z",
    "-v", $"{knowledge}:/data/knowledge:Z",
    "-v", $"{skills}:/data/skills:Z",
];

if (build)
{
    Info("Buildando imagen dev...");
    Exec(["podman", "build", "-t", Image, "-f", Path.Combine(root, "Containerfile.dev"), root]);
}

Silent(["podman", "volume", "create", NodeVolume]);

if (installDeps)
{
    Info("Instalando dependencias dentro del contenedor...");
    Exec(["podman", "run", "--rm", ..baseVols, Image,
        "sh", "-c", "cd /workspace/app && pnpm install --frozen-lockfile --ignore-scripts"]);
    Info("OK — dependencias instaladas.");
}
else if (buildFrontend)
{
    Info("Buildeando frontend dentro del contenedor...");
    Exec(["podman", "run", "--rm", ..baseVols, Image,
        "sh", "-c", "cd /workspace/app && pnpm run build"]);
    Info("OK — frontend buildeado.");
}
else
{
    Silent(["podman", "rm", "-f", Container]);
    Info("Iniciando dev server (dotnet watch)...");
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("Edita .cs en el host → hot reload automático");
    Console.WriteLine("Para cambios en app/ → en otra terminal:");
    Console.WriteLine("  dotnet run scripts/podman/podman-dev.cs -- --build-frontend");
    Console.ResetColor();
    Exec(["podman", "run", "-it", "--rm", "--name", Container, "-p", "5112:5111", ..baseVols, Image]);
}

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

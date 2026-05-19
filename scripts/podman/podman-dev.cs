// Levanta knowledge-search en modo desarrollo dentro de Podman.
// Source code editado en el host, compilado y ejecutado en el contenedor.
//
// Uso (desde cualquier directorio dentro del repo):
//   dotnet run scripts/podman/podman-dev.cs [-- <flags>]
//
//   --build            construye la imagen dev (primera vez o tras cambiar Containerfile.dev)
//   --install-deps     pnpm install dentro del contenedor (primera vez o tras cambiar package.json)
//   --build-frontend   pnpm run build dentro del contenedor (tras cambiar app/)
//   --test [filter]    corre `dotnet test` dentro del contenedor (one-off, --rm)
//   --bench <path>     corre el test de benchmark de ScanDirectory contra el
//                      repo en <path> del host (ver docs/benchmark.md)
//   (sin flags)        arranca dotnet watch con hot reload de .cs
//
// No requiere variables de entorno. Las fuentes se configuran desde la UI de Sources.
// Variables de entorno opcionales:
//   SKILLS_DIR  path a los skills de Claude (default: ~/.claude/skills)

using System.Diagnostics;
using System.Text.Json;

var build         = args.Contains("--build");
var installDeps   = args.Contains("--install-deps");
var buildFrontend = args.Contains("--build-frontend");
var test          = args.Contains("--test");
var testFilter    = test ? args.SkipWhile(a => a != "--test").Skip(1).FirstOrDefault(a => !a.StartsWith("--")) : null;
var bench         = args.Contains("--bench");
var benchPath     = bench ? args.SkipWhile(a => a != "--bench").Skip(1).FirstOrDefault(a => !a.StartsWith("--")) : null;
var detach        = args.Contains("--detach");

var root      = FindRoot();
var dataDir   = Path.Combine(root, "data-dev");
Directory.CreateDirectory(dataDir);
var sourcesJsonPath = Path.Combine(dataDir, "sources.json");
if (!File.Exists(sourcesJsonPath))
{
    File.WriteAllText(sourcesJsonPath, "{\n  \"version\": 1,\n  \"sources\": []\n}\n");
}
var sourceMounts = BuildSourceMounts(dataDir);
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
    ..sourceMounts,
    "-v", $"{skills}:/data/skills:Z",
    "-e", "SOURCES_CONFIG=/data/db/sources.json",
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
else if (bench)
{
    if (string.IsNullOrWhiteSpace(benchPath))
    {
        Console.Error.WriteLine("--bench requiere el path del repo del host. Ejemplo:");
        Console.Error.WriteLine("  dotnet run scripts/podman/podman-dev.cs -- --bench <hostRepoPath>");
        Environment.Exit(1);
    }
    var absoluteBenchPath = Path.GetFullPath(benchPath!);
    if (!Directory.Exists(absoluteBenchPath))
    {
        Console.Error.WriteLine($"El path no existe: {absoluteBenchPath}");
        Environment.Exit(1);
    }
    string[] benchVols =
    [
        "-v", $"{root}:/workspace:Z",
        "-v", $"{NodeVolume}:/workspace/app/node_modules",
        "-v", $"{absoluteBenchPath}:/bench-repo:Z,ro",
        "-e", "KNOWLEDGE_DB=/tmp/test-knowledge.db",
        "-e", "KNOWLEDGE_SEARCH_BENCH_REPO=/bench-repo",
    ];
    Info($"Benchmark con repo: {absoluteBenchPath}");
    Exec(["podman", "run", "--rm", ..benchVols, Image,
        "sh", "-c", "cd /workspace && unset SOURCES_CONFIG KNOWLEDGE_DIRS && rm -f /tmp/test-knowledge.db && dotnet test test/Api.Tests/Api.Tests.csproj --nologo --filter When_CodeGraphServiceBenchesRealRepository --logger \"console;verbosity=detailed\""]);
}
else if (test)
{
    // Tests en aislamiento: workspace + node_modules de pnpm. No montamos
    // data-dev/ — los tests no deben heredar SOURCES_CONFIG ni la DB del
    // entorno dev. KNOWLEDGE_DB se redirige a /tmp para que los tests que
    // levantan WebApplicationFactory puedan abrir SQLite en un directorio
    // existente; los tests que requieren aislamiento setean sus propios paths
    // via in-memory configuration (que pisa los env vars del runtime).
    string[] testVols =
    [
        "-v", $"{root}:/workspace:Z",
        "-v", $"{NodeVolume}:/workspace/app/node_modules",
        "-e", "KNOWLEDGE_DB=/tmp/test-knowledge.db",
    ];
    // NO pasamos SOURCES_CONFIG: AppConfiguration lo lee como env var con
    // mayor prioridad que el IConfiguration in-memory, y eso rompe la
    // hermeticidad de los tests que setean SourcesConfig por test.
    var filterArg = string.IsNullOrEmpty(testFilter) ? "" : $" --filter \"{testFilter}\"";
    Info($"Corriendo tests dentro del contenedor (filter: {testFilter ?? "<none>"})...");
    Exec(["podman", "run", "--rm", ..testVols, Image,
        "sh", "-c", $"cd /workspace && unset SOURCES_CONFIG KNOWLEDGE_DIRS && rm -f /tmp/test-knowledge.db && dotnet test test/Api.Tests/Api.Tests.csproj --nologo{filterArg}"]);
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
    string[] modeFlags = detach ? ["-d"] : ["-it", "--rm"];
    Exec(["podman", "run", ..modeFlags, "--name", Container, "-p", "5112:5111", ..baseVols, Image]);
}

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

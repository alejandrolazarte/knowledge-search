// Corre los tests Playwright dentro de un pod podman.
//
// Topologia:
//   pod knowledge-search-e2e
//     - knowledge-search-e2e-api    (imagen dev, expone :5120)
//     - knowledge-search-e2e-tests  (imagen playwright, ejecuta los specs)
//
// Uso (desde cualquier directorio dentro del repo):
//   dotnet run scripts/podman/podman-e2e.cs                       # corre todos los specs
//   dotnet run scripts/podman/podman-e2e.cs -- "Nombre del test"  # filtra con --grep
//
// Requisitos previos:
//   - Imagen dev construida: dotnet run scripts/podman/podman-dev.cs -- --build
//
// El pod se borra al terminar (incluso si los tests fallan). El exit code refleja
// el de Playwright.

using System.Diagnostics;

const string PlaywrightImage         = "mcr.microsoft.com/playwright:v1.59.1-noble";
const string ApiImage                = "localhost/knowledge-search-dev";
const string PodName                 = "knowledge-search-e2e";
const string ApiContainer            = "knowledge-search-e2e-api";
const string TestsContainer          = "knowledge-search-e2e-tests";
const string PnpmStoreVolume         = "knowledge-search-e2e-pnpm-store";
const string NodeModulesVolume       = "knowledge-search-e2e-node_modules";
const string RootNodeModulesVolume   = "knowledge-search-e2e-root-node_modules";
const string ApiPort                 = "5120";

var filter = args.FirstOrDefault(a => !a.StartsWith("--"));
var root   = FindRoot();

CleanupPreviousState(root);

Info("Creando pod...");
Exec(["podman", "pod", "create", "--name", PodName, "-p", $"{ApiPort}:{ApiPort}"]);

try
{
    Info("Iniciando API en contenedor...");
    Exec(["podman", "run", "-d", "--pod", PodName, "--name", ApiContainer,
        "-v", $"{root}:/workspace:Z",
        "-e", $"ASPNETCORE_URLS=http://+:{ApiPort}",
        "-e", "ASPNETCORE_ENVIRONMENT=E2E",
        "-e", "KNOWLEDGE_DB=/workspace/e2e/test.db",
        "-e", "SOURCES_CONFIG=/workspace/e2e/sources.json",
        ApiImage,
        "sh", "-c", "cd /workspace/src/Api && dotnet run --no-launch-profile"]);

    Info($"Esperando que la API responda en :{ApiPort}...");
    WaitForApi(ApiContainer, ApiPort);

    Info("Corriendo Playwright en contenedor...");
    var filterArg = string.IsNullOrEmpty(filter) ? "" : $" --grep \"{filter}\"";
    var script = string.Join(" && ",
        "cd /workspace/e2e",
        "npm install -g pnpm@11.1.2 --silent",
        "pnpm config set store-dir /pnpm-store",
        "CI=true pnpm install --force --ignore-scripts",
        $"unset CI && E2E_BASE_URL=http://localhost:{ApiPort} pnpm exec playwright test{filterArg}");

    var exitCode = ExecAndGetExitCode(["podman", "run", "--rm", "--pod", PodName,
        "--name", TestsContainer,
        "-v", $"{root}:/workspace:Z",
        "-v", $"{PnpmStoreVolume}:/pnpm-store",
        "-v", $"{RootNodeModulesVolume}:/workspace/node_modules",
        "-v", $"{NodeModulesVolume}:/workspace/e2e/node_modules",
        PlaywrightImage,
        "sh", "-c", script]);

    if (exitCode != 0)
    {
        Environment.Exit(exitCode);
    }
}
finally
{
    Info("Limpiando pod...");
    Silent(["podman", "pod", "rm", "-f", PodName]);
}

// ── helpers ──────────────────────────────────────────────────────────────────

static void CleanupPreviousState(string root)
{
    Silent(["podman", "pod", "rm", "-f", PodName]);

    var e2eDir = Path.Combine(root, "e2e");
    foreach (var name in new[] { "test.db", "test.db-wal", "test.db-shm", "sources.json" })
    {
        var p = Path.Combine(e2eDir, name);
        if (File.Exists(p))
        {
            try { File.Delete(p); } catch { }
        }
    }
}

static void WaitForApi(string apiContainer, string port)
{
    var deadline = DateTime.UtcNow.AddMinutes(3);
    while (DateTime.UtcNow < deadline)
    {
        var psi = new ProcessStartInfo("podman")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };
        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add(apiContainer);
        psi.ArgumentList.Add("sh");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add($"wget -qO- http://localhost:{port}/health || curl -sf http://localhost:{port}/health");
        using var process = Process.Start(psi);
        if (process is not null)
        {
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                return;
            }
        }
        Thread.Sleep(2000);
    }
    throw new TimeoutException($"La API no respondio en :{port} dentro del timeout.");
}

static int ExecAndGetExitCode(string[] args)
{
    var psi = new ProcessStartInfo(args[0]) { UseShellExecute = false };
    for (var i = 1; i < args.Length; i++) psi.ArgumentList.Add(args[i]);
    using var p = Process.Start(psi)!;
    p.WaitForExit();
    return p.ExitCode;
}

static string FindRoot()
{
    for (var d = Directory.GetCurrentDirectory(); d is not null; d = Path.GetDirectoryName(d))
        if (File.Exists(Path.Combine(d, "Containerfile"))) return d;
    throw new InvalidOperationException("Project root no encontrado (buscando Containerfile).");
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

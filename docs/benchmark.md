# Benchmark del indexador (`ScanDirectory`)

Test de integración que mide el tiempo end-to-end de `CodeGraphService.ScanDirectory`
contra un repositorio **real** del host (no un árbol sintético en tmpfs). Sirve para
validar mejoras en el walker o comparar `main` vs una rama de optimización.

## Por qué está skippeado por default

El test vive en `test/Api.Tests/When_CodeGraphServiceBenchesRealRepository.cs` y
está decorado con `[Fact]`, pero internamente chequea la env var
`KNOWLEDGE_SEARCH_BENCH_REPO`. Si no está seteada, **loggea `[skipped]` y retorna sin
ejercitar nada** — para que la suite normal (`--test`) no requiera repos montados ni
diferencie entre máquinas. Es un test pesado y de IO, no debe correr en CI ni en cada
cambio.

> No usamos `Skip.If` porque el proyecto no tiene la dependencia `Xunit.SkippableFact`;
> el chequeo manual hace el mismo trabajo y mantiene el csproj limpio.

## Cómo se corre

Desde la raíz del repo, vía el helper de Podman:

```powershell
dotnet run scripts/podman/podman-dev.cs -- --bench <ruta-al-repo-del-host>
```

`<ruta-al-repo-del-host>` es un path absoluto del **host** (Windows o Linux). El
script:

1. Lo resuelve a path absoluto.
2. Lo monta `ro` en `/bench-repo` dentro del container.
3. Setea `KNOWLEDGE_SEARCH_BENCH_REPO=/bench-repo`.
4. Corre `dotnet test --filter When_CodeGraphServiceBenchesRealRepository` con
   logger detallado para que aparezca el output del test.

> ⚠️ No commits paths reales. Cada quien usa los suyos al invocar el comando — este
> doc usa placeholders a propósito.

## Output

El test imprime via `ITestOutputHelper`:

```
Repo:         /bench-repo
Elapsed:      <ms>
FilesScanned: <n>
FilesSkipped: <n>
Nodes:        <n>
Edges:        <n>
```

## Flujo típico para comparar before/after

```powershell
# 1. Baseline en main
git switch main
dotnet run scripts/podman/podman-dev.cs -- --bench <ruta-al-repo>
# anotá "Elapsed" del output

# 2. Rama optimizada
git switch feature/optimize-indexer
dotnet run scripts/podman/podman-dev.cs -- --bench <ruta-al-repo>
# anotá "Elapsed" — comparar
```

Para hacer un caso pesado (frontend monorepo o monolito grande), repetir el
comando apuntando a esa otra ruta. El bind del host es la variable lenta — un
mismo repo medido dos veces en la misma máquina es comparable; entre máquinas
distintas no.

## Qué hace el test internamente

- Construye un `ConfiguredSource` mínimo apuntando al path inyectado.
- Llama `CodeGraphService.ScanDirectory(source)` con todos los parsers
  registrados (C#, TS/TSX/JS/JSX/MJS, Python).
- Mide elapsed con `Stopwatch`.
- Usa SQLite en `/tmp` para no ensuciar la DB de dev (`data-dev/`).

No verifica nodos esperados ni nada cualitativo — es solo timing.

# knowledge-search

Motor de búsqueda full-text para bases de conocimiento Markdown + grafo de relaciones de código fuente. Sirve como herramienta local para Claude Code vía HTTP.

## Stack

- **Backend**: ASP.NET Core Minimal API (.NET 10), C#
- **Persistencia**: SQLite con FTS5 (trigramas + BM25) para docs, tablas relacionales para grafo de código
- **Frontend**: React 19 + TypeScript + Tailwind (Vite)
- **Tests**: xUnit + Shouldly + Moq

## Estructura

```
src/Api/
  CodeGraph/Parsers/   ← parsers de lenguajes (C#, TS, Python...)
  Models/              ← records y enums de dominio
  Persistence/         ← IDbService + DbService (SQLite)
  Services/            ← ILogService, WatcherService
  Program.cs           ← composición raíz + endpoints
test/Api.Tests/        ← tests xUnit (TDD)
app/                   ← frontend React
data/                  ← SQLite DB + log (no va en la imagen)
```

## REGLA: siempre Podman, nunca local

Toda ejecución y compilación ocurre dentro de un contenedor Podman. Nunca ejecutar `dotnet run`, `dotnet watch run`, `pnpm install`, `pnpm run build` ni `pnpm run dev` directamente en el host. Si el usuario lo pide explícitamente, pedirle confirmación antes de proceder.

## Variables de entorno requeridas

Los scripts no tienen paths hardcodeados. Antes de usarlos:

```powershell
$env:KNOWLEDGE_DIR = "C:\ruta\a\tu\knowledge"   # requerido
$env:SKILLS_DIR    = "~\.claude\skills"          # opcional
```

## Uso productivo (sin tocar código)

```powershell
dotnet run scripts/podman/podman-run.cs                    # foreground con logs
dotnet run scripts/podman/podman-run.cs -- --detach        # arrancar en background
dotnet run scripts/podman/podman-run.cs -- --build         # rebuild imagen + arrancar
podman ps --filter name=knowledge-search
podman logs knowledge-search
podman stop knowledge-search
```

URL: `http://localhost:5111`

## Desarrollo activo (modificando código)

```powershell
dotnet run scripts/podman/podman-dev.cs -- --build --install-deps   # primera vez
dotnet run scripts/podman/podman-dev.cs                              # hot reload .cs
dotnet run scripts/podman/podman-dev.cs -- --build-frontend          # tras cambiar app/
dotnet run scripts/podman/podman-dev.cs -- --install-deps            # tras cambiar package.json
```

URL: `http://localhost:5112`  
`node_modules` vive en el volumen Podman `knowledge-search-node_modules` — nunca toca el host.

## Tests (permitidos sin aprobación, corren en el host)

```powershell
dotnet test
dotnet build src/Api/Api.csproj --no-restore
```

## Convenciones de código

### Nombres
- Sin abreviaturas: `sourceFilePath` no `src`, `fileExtension` no `ext`
- Sin comentarios: si sentís la necesidad de comentar, mejorá el nombre
- Nombres que comuniquen intención: clases, métodos, variables deben ser autodescriptivos

### Constantes
- Sin strings hardcodeados en lógica: usar constantes en la clase o una clase de constantes
- Las extensiones de archivo, nombres de columnas SQL y rutas van como constantes

### Diseño
- SOLID: una responsabilidad por clase, depender de abstracciones (interfaces)
- Cada parser de lenguaje implementa `ISourceFileParser`
- Cada servicio nuevo tiene su interfaz `I<Nombre>Service`

### Testing (TDD)
- **Siempre test primero**: el test falla (RED) antes de escribir implementación
- Convención de nombres: `When_<Sujeto><Verbo>` → `Then_<Comportamiento>`
- Tests aislados: sin dependencias externas reales (temp files para FS, in-memory para lógica pura)
- Un assert principal por test (puede haber asserts de apoyo)

### Documentación
- Si se documenta una decisión operativa, de seguridad o de infraestructura, crear un Markdown en `docs/`.
- Agregar un link desde `README.md` para que el documento sea descubrible.
- Cuando la app esté corriendo, llamar `POST /index` después de crear o actualizar docs para que aparezcan en Knowledge Search.
- Para cambios de configuración externa, documentar: estado inicial, comandos usados, por qué se aplicaron, estado final y comandos de auditoría.

## Variables de entorno

| Variable | Default | Descripción |
|---|---|---|
| `KNOWLEDGE_DB` | `../knowledge.db` | Path a la base SQLite |
| `KNOWLEDGE_DIRS` | `../knowledge` | Roots de docs Markdown (`;` separados) |
| `REPO_DIRS` | — | Roots de repositorios de código (`;` separados) |
| `SKILLS_DIR` | `~/.claude/skills` | Directorio de skills de Claude Code |

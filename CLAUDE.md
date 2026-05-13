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
- La guía actual de hardening de GitHub vive en `D:\Documentation\Projects\knowledge-search\github-hardening.md`.

## Comandos

```bash
# Correr tests
dotnet test

# Levantar API (hot reload)
cd src/Api && dotnet watch run

# Build frontend
cd app && npm run build
```

## Variables de entorno

| Variable | Default | Descripción |
|---|---|---|
| `KNOWLEDGE_DB` | `../knowledge.db` | Path a la base SQLite |
| `KNOWLEDGE_DIRS` | `../knowledge` | Roots de docs Markdown (`;` separados) |
| `REPO_DIRS` | — | Roots de repositorios de código (`;` separados) |
| `SKILLS_DIR` | `~/.claude/skills` | Directorio de skills de Claude Code |

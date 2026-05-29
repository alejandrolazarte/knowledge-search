# knowledge-search

Motor de búsqueda full-text para uno o más directorios de conocimiento y visor de skills. Indexa archivos Markdown en SQLite con FTS5 (trigramas + BM25). Incluye una UI en React + Tailwind con tema dark/light.

## Requisitos

- .NET 10 SDK (para correr los scripts de Podman)
- Podman Desktop o Podman CLI

No se necesita Node.js en el host. Todo lo demás compila y corre dentro del contenedor.

## Setup inicial — variables de entorno

Los scripts no contienen paths personales. Definí estas variables en tu perfil de PowerShell (`$PROFILE`):

```powershell
$env:KNOWLEDGE_DIRS = "C:\ruta\a\tu\carpeta\knowledge"  # requerido; varios roots separados por ;
$env:SKILLS_DIR    = "C:\Users\<tu-user>\.claude\skills" # opcional, se infiere por defecto
```

---

## Uso productivo

La app corre en un contenedor Podman. Los únicos directorios visibles desde el contenedor son los tres mounts explícitos — el resto de la máquina es inaccesible.

```powershell
cd knowledge-search

dotnet run scripts/podman/podman-run.cs                   # foreground (ver logs en vivo)
dotnet run scripts/podman/podman-run.cs -- --detach       # arrancar en background
dotnet run scripts/podman/podman-run.cs -- --build        # rebuild imagen + arrancar
```

Abre `http://localhost:5111`.

---

## Desarrollo activo

El source se edita en el host con tu editor. `dotnet watch`, `pnpm install` y `pnpm run build` corren dentro del contenedor. Los `node_modules` (raíz y `app/`) y el store de pnpm viven en volúmenes Podman aislados (`knowledge-search-root-node_modules`, `knowledge-search-node_modules`, `knowledge-search-pnpm-store`) y nunca tocan el host. El de la raíz es necesario para tapar el `node_modules` del host (Windows): si no, pnpm lo da por satisfecho y no instala los binarios Linux.

Por seguridad, `--install-deps` y `--build-frontend` corren con un set **mínimo** de mounts (solo `/workspace` + volúmenes de toolchain): no montan skills, repos indexados ni la DB, para que un paquete npm malicioso ejecutándose en `vite build` no los alcance. El dev server (`dotnet watch`) sí monta skills (rw, para editarlas desde la UI), repos y DB, porque corre tu código .NET, no paquetes npm.

```powershell
dotnet run scripts/podman/podman-dev.cs -- --build --install-deps   # primera vez
dotnet run scripts/podman/podman-dev.cs                              # hot reload (.cs)
dotnet run scripts/podman/podman-dev.cs -- --build-frontend          # tras cambiar app/
dotnet run scripts/podman/podman-dev.cs -- --install-deps            # tras cambiar package.json
```

Abre `http://localhost:5112`.

> `dotnet watch` usa polling (`DOTNET_USE_POLLING_FILE_WATCHER=true`) para detectar cambios a través de volúmenes Podman en Windows.

---

## Archivos de contenedor

| Archivo | Descripción |
|---|---|
| `Containerfile` | Build multi-stage: Node → .NET SDK → ASP.NET runtime (imagen productiva) |
| `Containerfile.dev` | .NET SDK 10 + Node 22 + pnpm, sin source code (imagen de desarrollo) |
| `scripts/podman/podman-run.cs` | Script C# — arranca el contenedor productivo |
| `scripts/podman/podman-dev.cs` | Script C# — orquesta imagen dev: build, pnpm, hot reload |
| `.containerignore` | Excluye `node_modules`, binarios, DB y archivos tmp del build context |
| `data/` | SQLite DB + log (montado en el contenedor, nunca incluido en la imagen) |

## Mounts

| Host | Contenedor | Contenido |
|---|---|---|
| `knowledge-search/data/` | `/data/db` | SQLite DB + log |
| `$env:KNOWLEDGE_DIRS` | `/data/knowledge` o `/data/knowledge/rootN` | Docs Markdown; uno o varios paths separados por `;` |
| `$env:SKILLS_DIR` (o `~/.claude/skills`) | `/data/skills` | Skills de Claude Code |

## Variables de entorno

| Variable | Valor en contenedor | Default local |
|---|---|---|
| `KNOWLEDGE_DB` | `/data/db/knowledge.db` | `../knowledge.db` |
| `KNOWLEDGE_DIRS` | `/data/knowledge` | `../knowledge`; acepta uno o varios roots separados por `;` |
| `SKILLS_DIR` | `/data/skills` | `~/.claude/skills` |

## API REST

| Endpoint | Descripción |
|---|---|
| `GET /search?q=texto&limit=5&modes=phrase,and,or&roots=knowledge` | Busca en el índice (BM25) |
| `POST /index` | Re-indexa el directorio (incremental) |
| `GET /roots` | Lista roots configurados |
| `GET /file?path=...` | Lee un archivo dentro de un root permitido |
| `GET /image?path=...` | Sirve imágenes locales referenciadas por Markdown |
| `GET /skills` | Lista de skills (nombre, descripción) |
| `GET /skills/{dir}` | Contenido crudo de una skill |
| `GET /health` | Health check |

## Vistas

- **Knowledge Search**: búsqueda FTS con BM25, filtros por root/modo, copia path con un clic, botón Re-index
- **Skills**: grid con todas las skills de `~/.claude/skills`, filtro por nombre/descripción, panel lateral con markdown renderizado

## Stack

- Backend: ASP.NET Core Minimal API (.NET 10), C#
- Persistencia: SQLite FTS5 (trigramas + BM25)
- Frontend: Vite 6 · React 19 · TypeScript 5 · Tailwind 3 · react-markdown
- Tests: xUnit + Shouldly + Moq

## Seguridad de dependencias

El frontend usa `pnpm` con configuración defensiva:

- `ignore-scripts=true` — no ejecuta scripts automáticos durante install
- `minimumReleaseAge: 4320` — espera 3 días antes de aceptar versiones recién publicadas
- `save-exact=true` — evita rangos al agregar dependencias

Combinado con el aislamiento de Podman, ningún paquete puede acceder al host más allá de los mounts explícitos.


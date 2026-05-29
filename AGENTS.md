# AGENTS.md

Instructions for Codex when working in this repository.

## Project

`knowledge-search` is a local full-text search and code graph tool.

- Backend: ASP.NET Core Minimal API (.NET 10), C#.
- Persistence: SQLite with FTS5 trigram/BM25 for Markdown docs, plus relational tables for code graph data.
- Frontend: React 19, TypeScript, Tailwind, Vite.
- Tests: xUnit, Shouldly, Moq.

## Repository Structure

```text
src/Api/
  CodeGraph/Parsers/   C#, TypeScript, Python parsers
  Models/              Domain records and enums
  Persistence/         IDbService and DbService
  Services/            ILogService, WatcherService
  Program.cs           Composition root and endpoint registration
test/Api.Tests/        xUnit tests
app/                   React frontend
data/                  SQLite DB + log (never included in image)
docs/                  Project documentation and runbooks
```

## RULE: always Podman, never local

All execution and compilation happens inside a Podman container. Never run `dotnet run`, `dotnet watch run`, `pnpm install`, `pnpm run build`, or `pnpm run dev` directly on the host. If the user explicitly requests it, ask for confirmation before proceeding.

## Environment variables (required before running scripts)

Scripts contain no personal paths. Set these before use:

```powershell
$env:KNOWLEDGE_DIRS = "C:\path\to\your\knowledge"  # required; multiple roots separated by ;
$env:SKILLS_DIR    = "~\.claude\skills"             # optional, inferred by default
```

## Production use (no code changes)

```powershell
dotnet run scripts/podman/podman-run.cs                    # foreground with logs
dotnet run scripts/podman/podman-run.cs -- --detach        # start in background
dotnet run scripts/podman/podman-run.cs -- --build         # rebuild image + start
podman ps --filter name=knowledge-search
podman logs knowledge-search
podman stop knowledge-search
```

URL: `http://localhost:5111`

## Active development (editing code)

Source code is edited on the host. Compilation and execution happen inside the container. `node_modules` (root and `app/`) and the pnpm store live in isolated Podman volumes (`knowledge-search-root-node_modules`, `knowledge-search-node_modules`, `knowledge-search-pnpm-store`) and never touch the host filesystem. The root volume is required to mask the host's (Windows) `node_modules`; otherwise pnpm treats it as satisfied and skips installing the Linux binaries.

For safety, `--install-deps` and `--build-frontend` run with a **minimal** mount set (only `/workspace` + toolchain volumes): no skills, indexed repos, or DB, so a malicious npm package executing during `vite build` cannot reach them. The dev server (`dotnet watch`) does mount skills (rw, for editing from the UI), repos, and DB, because it runs your .NET code, not npm packages.

```powershell
dotnet run scripts/podman/podman-dev.cs -- --build --install-deps   # first time setup
dotnet run scripts/podman/podman-dev.cs                              # hot reload (.cs)
dotnet run scripts/podman/podman-dev.cs -- --build-frontend          # after changing app/
dotnet run scripts/podman/podman-dev.cs -- --install-deps            # after changing package.json
```

URL: `http://localhost:5112`

## Tests (always allowed, run on host)

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal
dotnet build src\Api\Api.csproj --no-restore -v minimal
```

## Coding Conventions

- Use descriptive names. Avoid abbreviations like `src` or `ext` when a clearer name fits.
- Prefer self-explanatory code over comments.
- Do not hardcode repeated strings in logic; use constants for file extensions, SQL column names, and route fragments.
- Keep classes focused on one responsibility.
- New services should have an interface named `I<Name>Service`.
- Each language parser implements `ISourceFileParser`.
- Keep domain models free of API/application response DTOs. Domain types represent behavior and core concepts; use case response models belong under `Core.UseCases`, and HTTP-only models stay in `Api`.
- Use Core use cases for application orchestration. Minimal API endpoints should bind HTTP inputs, call a use case, and map `Result<T>` to HTTP.
- Use `Result` for expected outcomes such as validation errors, not found, conflicts, and unauthorized results.
- Do not catch repository, SQLite, or Core exceptions in repositories, adapters, or use cases by default. Let unexpected exceptions bubble to the global exception handler, which logs them and returns a friendly error response.
- Keep concrete IO, SQLite, ASP.NET, and hosting concerns out of Core. Core depends on abstractions; `Api` owns the concrete adapters.
- Prefer `Result<T>.Resolve(...)` at boundaries when converting success/failure outcomes into transport responses.

## Testing

- Follow TDD for behavior changes when practical: write the failing test first, then implementation.
- Test names follow `When_<Subject><Verb>` with `Then_<Behavior>` assertions.
- Keep tests isolated. Use temp files for filesystem tests and avoid external services.
- Run focused tests for the changed area before claiming completion.

## Documentation

When documenting an operational, security, infrastructure, or external configuration decision:

1. Create or update a Markdown file in `docs/`.
2. Link it from `README.md` when it should be discoverable from the repo root.
3. Include the initial state, exact commands used, why each change was made, the final state, and audit commands.


## Git Workflow

`main` is protected in GitHub.

- Do not push directly to `main`.
- Create branches with the `codex/` prefix for Codex work.
- Commit on the branch and push the branch.
- Open a Pull Request for merge into `main`.
- The repo ruleset requires PRs and prevents direct pushes/bypass.

Before pushing, check:

```powershell
git status --short --branch
git diff --stat
```

## GitHub Security Notes

The repo is hardened to reduce PR/Actions risk:

- `main` requires Pull Requests.
- No bypass actors are configured.
- GitHub Actions default token permission is read-only.
- Only GitHub-owned Actions are allowed.
- External contributor PR workflows require manual approval.

Audit command:

```powershell
gh api repos/alejandrolazarte/knowledge-search/rulesets/16156415
```

## Frontend Notes

- Match existing React/Tailwind style.
- Keep tool UIs dense and usable; avoid landing-page composition.
- Use existing shared components before creating new ones.
- The frontend uses `pnpm`, not npm.
- All pnpm commands (`install`, `build`, `dev`) must run inside the container via `scripts/podman/podman-dev.cs`.

## Backend Notes

- Keep endpoint files focused by feature area.
- Prefer shared query builders/helpers over duplicating FTS query construction.
- SQLite FTS search modes should stay consistent between Knowledge Search and Repo Search.
- Avoid changing Code Graph behavior unless the task explicitly requires it.

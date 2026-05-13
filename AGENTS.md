# AGENTS.md

Instructions for Codex when working in this repository.

## Project

`knowledge-search` is a local full-text search and code graph tool.

- Backend: ASP.NET Core Minimal API (.NET 10), C#.
- Persistence: SQLite with FTS5 trigram/BM25 for Markdown docs, plus relational tables for code graph data.
- Frontend: React 19, TypeScript, Tailwind, Vite.
- Tests: xUnit, Shouldly, Moq.

The app indexes Markdown documentation from `D:\Documentation` and can scan source repositories to build a code graph plus repo code search.

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
docs/                  Project documentation and runbooks
```

## Coding Conventions

- Use descriptive names. Avoid abbreviations like `src` or `ext` when a clearer name fits.
- Prefer self-explanatory code over comments.
- Do not hardcode repeated strings in logic; use constants for file extensions, SQL column names, and route fragments.
- Keep classes focused on one responsibility.
- New services should have an interface named `I<Name>Service`.
- Each language parser implements `ISourceFileParser`.

## Testing

- Follow TDD for behavior changes when practical: write the failing test first, then implementation.
- Test names follow `When_<Subject><Verb>` with `Then_<Behavior>` assertions.
- Keep tests isolated. Use temp files for filesystem tests and avoid external services.
- Run focused tests for the changed area before claiming completion.

Useful commands:

```powershell
dotnet test test\Api.Tests\Api.Tests.csproj --no-restore -v minimal
dotnet build src\Api\Api.csproj --no-restore -v minimal
cd app
npm run build
```

## Documentation

When documenting an operational, security, infrastructure, or external configuration decision:

1. Create or update a Markdown file in `docs/`.
2. Link it from `README.md` when it should be discoverable from the repo root.
3. Include the initial state, exact commands used, why each change was made, the final state, and audit commands.
4. If the running app should surface the document through Knowledge Search, also add/update the corresponding document under `D:\Documentation`.
5. When touching `D:\Documentation`, follow its own `D:\Documentation\AGENTS.md`: update `index.md` and `log.md`.

Current runbook:

```text
D:\Documentation\Projects\knowledge-search\github-hardening.md
```

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

Expected important fields:

```json
{
  "enforcement": "active",
  "bypass_actors": [],
  "current_user_can_bypass": "never"
}
```

## Frontend Notes

- Match existing React/Tailwind style.
- Keep tool UIs dense and usable; avoid landing-page composition.
- Use existing shared components before creating new ones.
- Run `npm run build` after TypeScript/frontend changes.

## Backend Notes

- Keep endpoint files focused by feature area.
- Prefer shared query builders/helpers over duplicating FTS query construction.
- SQLite FTS search modes should stay consistent between Knowledge Search and Repo Search.
- Avoid changing Code Graph behavior unless the task explicitly requires it.

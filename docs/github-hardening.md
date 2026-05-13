# GitHub Hardening Del Repo

Fecha: 2026-05-13

Repo: `alejandrolazarte/knowledge-search`

Objetivo:

- Proteger `main` para que no se pueda pushear directo.
- Obligar a pasar por Pull Request.
- Mantener el repo usable para trabajo personal: el owner puede crear PRs, aprobarlos y mergearlos.
- Reducir riesgo de ataques por Pull Requests y GitHub Actions.
- Evitar que workflows o actions maliciosas roben credenciales o escriban al repo.

## Estado Inicial

Primero se verifico el remoto:

```powershell
git remote -v
```

Resultado esperado:

```text
origin  https://github.com/alejandrolazarte/knowledge-search.git (fetch)
origin  https://github.com/alejandrolazarte/knowledge-search.git (push)
```

Se verifico que `gh` estuviera disponible:

```powershell
gh --version
```

Se consultaron los rulesets activos:

```powershell
gh api repos/alejandrolazarte/knowledge-search/rulesets
```

Habia un ruleset activo:

```text
id: 16156415
name: main protection
target: branch
enforcement: active
```

Despues se pidio el detalle:

```powershell
gh api repos/alejandrolazarte/knowledge-search/rulesets/16156415
```

El problema encontrado fue este:

```json
"bypass_actors": [
  {
    "actor_id": 5,
    "actor_type": "RepositoryRole",
    "bypass_mode": "always"
  }
],
"current_user_can_bypass": "always"
```

Eso explicaba por que se pudo hacer push directo a `main` aunque la regla dijera que los cambios debian entrar por Pull Request.

## Proteccion De `main`

Primero se quito el bypass del ruleset.

Comando usado:

```powershell
@'
{"name":"main protection","target":"branch","enforcement":"active","conditions":{"ref_name":{"exclude":[],"include":["~DEFAULT_BRANCH"]}},"rules":[{"type":"deletion"},{"type":"pull_request","parameters":{"required_approving_review_count":1,"dismiss_stale_reviews_on_push":false,"required_reviewers":[],"require_code_owner_review":false,"require_last_push_approval":false,"required_review_thread_resolution":false,"allowed_merge_methods":["merge","squash","rebase"]}},{"type":"non_fast_forward"}],"bypass_actors":[]}
'@ | gh api repos/alejandrolazarte/knowledge-search/rulesets/16156415 --method PUT --input -
```

Que hace:

- `enforcement: active`: la regla esta activa.
- `include: ["~DEFAULT_BRANCH"]`: aplica a la rama default, que es `main`.
- `deletion`: impide borrar `main`.
- `pull_request`: exige PR para entrar a `main`.
- `required_approving_review_count: 1`: exige una aprobacion.
- `require_last_push_approval: false`: permite que el owner pueda aprobar/mergear sus propios PRs cuando trabaja solo.
- `non_fast_forward`: impide force push.
- `bypass_actors: []`: nadie puede saltarse la regla.

Estado final verificado:

```json
"bypass_actors": [],
"current_user_can_bypass": "never"
```

Importante:

- Esta configuracion no permite push directo a `main`.
- Permite que el owner trabaje con PRs propios y los mergee.
- Si hubiera otros colaboradores con permisos de merge, tambien podrian mergear PRs que cumplan las reglas. Para que solo el owner pueda mergear, hay que controlar permisos de colaboradores o agregar reglas mas estrictas con CODEOWNERS.

## Revision De Ramas

Se listaron las ramas del repo:

```powershell
gh api repos/alejandrolazarte/knowledge-search/branches --paginate
```

Se confirmo que la rama protegida real es `main`. No habia rama `master`.

## Revision De Colaboradores

Se revisaron colaboradores y permisos:

```powershell
gh api repos/alejandrolazarte/knowledge-search/collaborators --paginate
```

Estado observado:

```json
{
  "login": "alejandrolazarte",
  "role_name": "admin",
  "permissions": {
    "admin": true,
    "maintain": true,
    "push": true,
    "triage": true,
    "pull": true
  }
}
```

Conclusion:

- Solo aparece `alejandrolazarte` como colaborador/admin.
- No habia otros usuarios con permisos directos sobre el repo.

## Revision De GitHub Actions

Se reviso la configuracion general de Actions:

```powershell
gh api repos/alejandrolazarte/knowledge-search/actions/permissions
```

Estado inicial observado:

```json
{
  "enabled": true,
  "allowed_actions": "all",
  "sha_pinning_required": false
}
```

Riesgo:

- `allowed_actions: all` permite usar cualquier Action del marketplace.
- Si alguien logra introducir un workflow malicioso en un PR o en una rama con permisos, podria intentar ejecutar codigo no confiable.

Se revisaron permisos del token de Actions:

```powershell
gh api repos/alejandrolazarte/knowledge-search/actions/permissions/workflow
```

Estado observado:

```json
{
  "default_workflow_permissions": "read",
  "can_approve_pull_request_reviews": false
}
```

Esto ya estaba bien:

- `GITHUB_TOKEN` queda en solo lectura por default.
- Los workflows no pueden aprobar PRs.

Se revisaron secrets del repo:

```powershell
gh api repos/alejandrolazarte/knowledge-search/actions/secrets --paginate
```

Estado observado:

```json
{
  "total_count": 0,
  "secrets": []
}
```

Esto tambien estaba bien:

- No habia secrets de repo expuestos a workflows.

## Revision Del Workflow Actual

Se buscaron workflows:

```powershell
rg --files .github
```

Resultado:

```text
.github\workflows\ci.yml
```

Se buscaron actions y patrones peligrosos:

```powershell
rg -n "uses:|permissions:|pull_request_target|secrets|workflow_run|checkout|setup" .github
```

Actions usadas:

```text
actions/checkout@v4
actions/setup-dotnet@v4
actions/setup-node@v4
actions/upload-artifact@v4
```

Conclusion:

- El workflow actual solo usa actions oficiales de GitHub (`actions/*`).
- No se encontro `pull_request_target`.
- No se encontraron referencias directas a `secrets`.

## Restringir Actions Permitidas

Se cambio GitHub Actions para no permitir cualquier action del marketplace.

Primero se paso de `all` a `selected`:

```powershell
@'
{"enabled":true,"allowed_actions":"selected"}
'@ | gh api repos/alejandrolazarte/knowledge-search/actions/permissions --method PUT --input -
```

Despues se permitieron solo actions oficiales de GitHub:

```powershell
@'
{"github_owned_allowed":true,"verified_allowed":false,"patterns_allowed":[]}
'@ | gh api repos/alejandrolazarte/knowledge-search/actions/permissions/selected-actions --method PUT --input -
```

Que hace:

- `github_owned_allowed: true`: permite actions oficiales de GitHub, como `actions/checkout`.
- `verified_allowed: false`: no permite automaticamente actions de creadores verificados externos.
- `patterns_allowed: []`: no agrega excepciones manuales.

Estado final verificado:

```powershell
gh api repos/alejandrolazarte/knowledge-search/actions/permissions
```

```json
{
  "enabled": true,
  "allowed_actions": "selected"
}
```

Y:

```powershell
gh api repos/alejandrolazarte/knowledge-search/actions/permissions/selected-actions
```

```json
{
  "github_owned_allowed": true,
  "patterns_allowed": [],
  "verified_allowed": false
}
```

## Proteger Workflows De PRs Externos

Se reviso la politica para workflows de PRs desde forks/contribuidores externos:

```powershell
gh api repos/alejandrolazarte/knowledge-search/actions/permissions/fork-pr-contributor-approval
```

Estado inicial:

```json
{
  "approval_policy": "first_time_contributors"
}
```

Riesgo:

- Despues de que un contribuidor externo ya fue aprobado una vez, futuros workflows podrian correr con menos friccion.

Se cambio a requerir aprobacion para todos los contribuidores externos:

```powershell
@'
{"approval_policy":"all_external_contributors"}
'@ | gh api repos/alejandrolazarte/knowledge-search/actions/permissions/fork-pr-contributor-approval --method PUT --input -
```

Estado final verificado:

```json
{
  "approval_policy": "all_external_contributors"
}
```

## Estado Final Deseado

Con esta configuracion, el repo queda asi:

- `main` no acepta push directo.
- `main` requiere Pull Request.
- No hay bypass para saltarse la regla.
- No se puede borrar `main`.
- No se puede hacer force push a `main`.
- Se requiere 1 approval para mergear.
- El owner puede aprobar/mergear sus propios PRs si trabaja solo.
- `GITHUB_TOKEN` de Actions queda en solo lectura.
- Actions solo permite actions oficiales de GitHub.
- PRs externos requieren aprobacion manual antes de correr workflows.
- No hay secrets configurados en el repo.
- Solo `alejandrolazarte` aparece como colaborador/admin.

## Comandos De Auditoria Rapida

Ver ruleset:

```powershell
gh api repos/alejandrolazarte/knowledge-search/rulesets/16156415
```

Ver permisos de Actions:

```powershell
gh api repos/alejandrolazarte/knowledge-search/actions/permissions
```

Ver actions seleccionadas:

```powershell
gh api repos/alejandrolazarte/knowledge-search/actions/permissions/selected-actions
```

Ver permisos del token de workflow:

```powershell
gh api repos/alejandrolazarte/knowledge-search/actions/permissions/workflow
```

Ver politica de PRs externos:

```powershell
gh api repos/alejandrolazarte/knowledge-search/actions/permissions/fork-pr-contributor-approval
```

Ver colaboradores:

```powershell
gh api repos/alejandrolazarte/knowledge-search/collaborators --paginate
```

Buscar patrones riesgosos en workflows:

```powershell
rg -n "pull_request_target|secrets|workflow_run|uses:|permissions:" .github
```

## Reglas Practicas Para El Futuro

- No usar `pull_request_target` salvo que sea estrictamente necesario y nunca para ejecutar codigo del PR.
- No agregar secrets a workflows que corran codigo de PRs externos.
- Si se agrega una action de terceros, preferir pinnearla por SHA y documentar por que se confia en ella.
- Revisar `permissions:` dentro de cada workflow y job; preferir permisos minimos.
- Mantener `GITHUB_TOKEN` en `read` por default.
- Mantener PRs externos con aprobacion manual antes de correr Actions.
- No agregar colaboradores con `admin` o `write` salvo que sea necesario.

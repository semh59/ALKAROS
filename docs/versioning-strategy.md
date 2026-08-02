# Versioning Strategy

> **Based on:** GATES.md, TASK_STANDARD.md
> **Date:** 2026-07-30

## 1. Branching Model

```
main (protected)
  ├── v0/          (V0 validation & architecture closure)
  ├── v1/          (V1 foundation + core features)
  ├── v1.1/        (V1.1 inventory, recipes, production)
  ├── v1.2/        (V1.2 payments, fiscal, cash)
  ├── v1.3/        (V1.3 accounts, invoicing)
  ├── v1.4/        (V1.4 online ordering, QR, channels)
  ├── v1.5/        (V1.5 hardening, recovery, observability)
  └── v2.0/        (V2.0 release, certification, go-live)
```

### Branch Rules

| Branch | Source | Merge Target | Protection |
|--------|--------|-------------|------------|
| `main` | — | — | Protected. Requires gate approval |
| `v0/*` | `main` | `main` | Feature branches off `v0/` |
| `v1/*` | `main` | `main` | Only after GATE-V0-EXIT |
| `v1.1/*` | `main` | `main` | Only after GATE-V1-EXIT |
| `v1.2/*` | `main` | `main` | Only after GATE-V11-EXIT |
| `v1.3/*` | `main` | `main` | Only after GATE-V12-EXIT |
| `v1.4/*` | `main` | `main` | Only after GATE-V13-EXIT |
| `v1.5/*` | `main` | `main` | Only after GATE-V14-EXIT |
| `v2.0/*` | `main` | `main` | Only after GATE-V15-EXIT |

### Task Branches

Each task gets a branch from its version base:
```
git checkout -b V0-DOM-001-lifecycle-transitions
git checkout -b V1-FND-001-module-skeleton
git checkout -b V12-PAY-001-payment-aggregate
```

Branch naming: `<TASK-ID>-<short-description>` (branch off the task's version base)

## 2. Commit Convention

```
<type>(<scope>): <description>

[optional body]

Task: <TASK-ID>
Gate: <GATE-ID>
```

### Types
| Type | Usage |
|------|-------|
| `decision` | V0 decision record |
| `validation` | V0 validation evidence |
| `feat` | New feature implementation |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `refactor` | Code refactoring |
| `test` | Test addition/update |
| `chore` | Build, CI, tooling |
| `gate` | Gate approval/closure |

### Examples
```
decision(domain): define lifecycle transition contracts

Task: V0-DOM-001
Gate: GATE-V0-ENTRY
```

```
feat(billing): implement bill aggregate with split support

- Bill aggregate root with event sourcing
- Split command with idempotency
- Unit tests for all cardinality scenarios

Task: V1-BIL-001
Gate: GATE-V1-ENTRY
```

## 3. Tagging Strategy

### Version Tags
```
v0.0.0          # V0 baseline (initial commit)
v0.1.0          # V0 all decision records complete
v0.2.0          # V0 all validation evidence complete
v0.9.0          # V0 all tasks Done or Blocked
v1.0.0-rc.1     # GATE-V0-EXIT closed, V1 foundation started
v1.0.0          # V1 complete
v1.1.0          # V1.1 complete
v1.2.0          # V1.2 complete
v1.3.0          # V1.3 complete
v1.4.0          # V1.4 complete
v1.5.0          # V1.5 complete
v2.0.0-rc.1     # Release candidate
v2.0.0          # Production release (GATE-V20-EXIT)
```

### Gate Tags
```
gate/v0-entry    # GATE-V0-ENTRY verified
gate/v0-exit     # GATE-V0-EXIT closed
gate/v1-entry    # GATE-V1-ENTRY closed
gate/v1-exit     # GATE-V1-EXIT closed
...
gate/v20-exit    # GATE-V20-EXIT closed
```

## 4. Current State

| Tag | Status | Commit |
|-----|--------|--------|
| `v0.0.0` | ✅ Done | `655d0b2` (docs(versioning) commit) |
| `gate/v0-entry` | ✅ Done | `655d0b2` (PDF verified, sources registered) |

2026-08-01 tarihli düzeltme (V1-FND-009 kapsamı, V0-DOM-001 sahipliğinde): `v0.0.0` ve
`gate/v0-entry` tag'lerinin ikisi de `655d0b2` commit'inde doğrulandı (annotated tag objeleri
`39ccb50`/`e302c4c`); önceki tablo yanlışlıkla `fc5ae22` yazıyordu. Geçmiş yeniden yazımında
bu commit `Task: V0-DOM-001` footer'ı zaten mevcut olduğu için SHA değişmedi.

## 5. Workflow

1. **V0 phase**: All work on `main` (no branches needed for decision records)
2. **V1+ phase**: Each task gets a feature branch from `main`
3. **Gate closure**: Gate tag created, branch merged to `main` with signed commit
4. **Release**: Version tag created, release notes generated from commit history
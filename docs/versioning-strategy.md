# Versioning Strategy

> **Based on:** GATES.md, TASK_STANDARD.md
> **Date:** 2026-08-11

## 1. Branching Model

```text
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
| -------- | -------- | ------------- | ------------ |
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

```text
git checkout -b V0-DOM-001-lifecycle-transitions
git checkout -b V1-FND-001-module-skeleton
git checkout -b V12-PAY-001-payment-aggregate
```

Branch naming: `<TASK-ID>-<short-description>` (branch off the task's version base)

## 2. Commit Convention

```text
<type>(<scope>): <description>

[optional body]

Task: <TASK-ID>
Gate: <GATE-ID>
```

`Task:` ve `Gate:` satırları commit mesajının sonunda tek, bitişik bir trailer
bloğu oluşturur. İlk trailer'dan önce boş bir ayırıcı satır bulunur; trailer
satırlarının arasında boş satır, literal `\n` karakterleri veya serbest metin
bulunmaz. Böylece `git interpret-trailers --parse` iki alanı da ayrıştırır.

### Types

| Type | Usage |
| ------ | ------- |
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

```text
decision(domain): define lifecycle transition contracts

Task: V0-DOM-001
Gate: GATE-V0-ENTRY
```

```text
feat(billing): implement bill aggregate with split support

- Bill aggregate root with event sourcing
- Split command with idempotency
- Unit tests for all cardinality scenarios

Task: V1-BIL-001
Gate: GATE-V1-ENTRY
```

## 3. Tagging Strategy

### Version Tags

```text
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

```text
gate/v0-entry    # GATE-V0-ENTRY verified
gate/v0-exit     # GATE-V0-EXIT closed
gate/v1-entry    # GATE-V1-ENTRY closed
gate/v1-exit     # GATE-V1-EXIT closed
...
gate/v20-exit    # GATE-V20-EXIT closed
```

## 4. Current State

| Tag | Status | Commit |
| ----- | -------- | -------- |
| `v0.0.0` | ✅ Done | `46c8d7d` (docs(versioning) commit) |
| `gate/v0-entry` | ✅ Done | `46c8d7d` (PDF verified, sources registered) |

2026-08-01 tarihli düzeltme (V1-FND-009 kapsamı, V0-DOM-001 sahipliğinde): `v0.0.0` ve
`gate/v0-entry` tag'lerinin ikisi de `655d0b2` commit'inde doğrulandı (annotated tag objeleri
`39ccb50`/`e302c4c`); önceki tablo yanlışlıkla `fc5ae22` yazıyordu. Geçmiş yeniden yazımında
bu commit `Task: V0-DOM-001` footer'ı zaten mevcut olduğu için SHA değişmedi.

2026-08-05 tarihli ikinci düzeltme (V1-FND-009 C45 kapsamı, V0-DOM-001 sahipliğinde): tam geçmiş
yeniden yazımı sonrası her iki tag de `46c8d7d` (yeni kök baseline, eski `fc5ae22`'nin yeniden
yazılmış hâli) commit'ine işaret eder; annotated tag objeleri `f6efc80`/`439e4e8` olarak yeniden
oluşturuldu. C45'teki `125 commit` değeri o günün tarihsel ölçümüdür; güncel sayı değildir.

### Immutable history attestation (CORR:C52)

2026-08-11 tarihinde `V0-GOV-038`, `8d466ba` kökünden
`0c8cd75` candidate'ına kadar **157 commit** ölçtü. C52'nin dondurulmuş
145-commit denetim satırı SHA-256 ile doğrulandı; candidate'a sonradan eklenen
12 commit aynı ledger'a canlı Git nesnelerinden eklendi. Bu iki sayı farklı
tarihlere aittir; C45'in 125-commit tarihi güncel tarihçe sayımı gibi
kullanılamaz.

- Commit-time scope sonucu 45 `FAIL` satırıdır.
- C52 current-contract snapshot sonucu 53 `FAIL` satırıdır. Retrospective ve
  current verdict'ler aynı alan değildir ve birbirinin yerine geçmez.
- 13 C52 missing-footer commit'i exact SHA listesiyle
  `evidence/V0-GOV-038/controls.md` içinde tutulur.
- `2afa0c3` literal `\n` trailer metni ve `0f2efe6` ayrık trailer bloğu,
  yeniden yazılmayacak immutable istisnalardır. Bu istisnalar 13-commit C52
  footer kümesinden ayrıdır.

Attestation, rebase, amend, force-push, tag taşıma veya eski commit mesajını
değiştirme yetkisi vermez. Her gelecekteki commit bu belgedeki canonical trailer
bloğunu kullanır; geçmiş bulgular yeni task kapsamındaki kanıt veya remediation
ile ele alınır. Tam changed-path ledger, commit-time dependency durumları ve
before/after history fingerprint'i `evidence/V0-GOV-038/` altındadır.

## 5. Workflow

1. **V0 phase**: All work on `main` (no branches needed for decision records)
2. **V1+ phase**: Each task gets a feature branch from `main`
3. **Gate closure**: Gate tag created, branch merged to `main` with signed commit
4. **Release**: Version tag created, release notes generated from commit history

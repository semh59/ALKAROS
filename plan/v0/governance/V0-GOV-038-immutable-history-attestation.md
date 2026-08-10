# V0-GOV-038 - Attest immutable commit history

- Task ID: V0-GOV-038
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

Root commit'ten candidate HEAD'e kadar Task/Gate trailer ve commit-time scope sonuçlarını geçmişi yeniden yazmadan kanıtlamak; current ve retrospective sözleşmeleri ayırmak.

## Owned surface

- `docs/versioning-strategy.md`
- `evidence/V0-GOV-038/**`

## In scope

- Her commit için canonical trailers, changed paths, o tarihteki allowlist ve gate/dependency durumunu ledger'a yazmak.
- 45 commit-time scope ihlali ve 13 footer sorununun exact commit kümelerini yeniden ölçmek.
- Stale commit-count iddialarını current count ile tarihli olarak değiştirmek.

## Out of scope

- Rebase, amend, force-push veya geçmiş commit mesajı/diff'i değiştirmek.
- Tarihsel uyumsuzluğu current dirty worktree sayısıyla birleştirmek.

## Dependencies

- V0-GOV-035

## Deliverables

- Immutable history ledger, exception sınıflandırması ve güncel versioning strategy açıklaması.

## Acceptance evidence

- Root..candidate bütün commitler ledger'da tam bir satıra bağlıdır.
- Commit-time ve current-contract verdict'leri ayrı sütunlardadır; 45 scope ve 13 footer kaydı exact SHA listesiyle doğrulanır.
- Repository history hash'leri task öncesi/sonrası aynıdır.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V0-GOV-038/**` altındadır.

## Handoff

- V1-CAT-004
- V0-GOV-045

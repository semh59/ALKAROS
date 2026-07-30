# V15-KVK-002 - Implement cross-store anonymization

- Task ID: V15-KVK-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.11-II.12
- PDF:III.33-III.34

## Goal

Onaylı PII anonymization işlemini idempotent, resumable ve store-checkpoint tabanlı workflow olarak uygulamak.

## Owned surface

- `src/Modules/Privacy/Anonymization/**`, `tests/Modules/Privacy/Anonymization/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Per-field action, store checkpoint, retry/resume, referential integrity, audit entry ve final all-store verification.

## Out of scope

- Saklama planlaması ve şifreleme anahtarı rotasyonu.

## Dependencies

- V15-KVK-001
- V13-CST-002
- V15-SEC-003
- V1-OPS-001

## Deliverables

- `src/Modules/Privacy/Anonymization/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Seeded subject data her in-scope store checkpoint'inden sonra kaldırılır; interrupted workflow aynı noktadan güvenle
  devam eder; financial totals ve legal IDs geçerli kalır.

## Handoff

- V20-CMP-001

# V1-FND-005 - Implement transaction execution boundary

- Task ID: V1-FND-005
- Status: Done
- Assignee: opencode-v1-fnd-005
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.7-I.10
- PDF:I.49
- PDF:II.4
- CORR:C4

## Goal

V0-ARC-001 ve V0-ARC-003 kararlarını tek transaction, rollback ve retry yürütme primitive'lerinde uygulamak.

## Owned surface

- Kapsam genişletme onayı (2026-07-31 kullanıcı talimatı): bu task'ın yeni projelerinin `ALKAROS.slnx` ve
  `build/project-manifest.json` içine kaydı.
- Bu görev domain workflow veya module repository yüzeyini değiştiremez.

## In scope

- Transaction propagation, nested-call rejection, commit/rollback ve retry classification.

## Out of scope

- Inbox/Outbox persistence/dispatch, post-commit event handoff, provider network çağrısı, domain-specific compensation
  ve
  module schema.

## Dependencies

- V1-FND-004
- V0-ARC-001
- V0-ARC-003

## Deliverables

- `src/BuildingBlocks/Transactions/**` altında transaction execution production code'u.
- Concurrency, rollback, crash-window ve retry classification testleri.

## Acceptance evidence

- Aynı workflow içindeki module writes tek commit veya tam rollback üretir.
- Bilinmeyen hata otomatik retry edilmez ve nested bağımsız transaction açılması reddedilir.

## Handoff

- V1-SEC-001
- V1-FND-006
- V1-TBL-002
- V12-PAY-004
- V12-ALC-004

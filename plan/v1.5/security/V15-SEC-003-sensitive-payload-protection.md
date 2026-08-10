# V15-SEC-003 - Harden sensitive payload retention

- Task ID: V15-SEC-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.38-I.44
- PDF:II.11-II.12
- PDF:III.33-III.34

## Goal

V1-SEC-002 sınırı üzerinde retention enforcement, authorized re-encryption ve deletion scheduling uygulamak.

## Owned surface

- `src/Modules/Security/DataProtectionRetention/**`, `tests/Modules/Security/DataProtectionRetention/**`,
  `database/migrations/V15/V15-SEC-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Retention execution, authorized re-encryption, deletion queue, legal hold conflict ve coverage verification.
- Provider payload retention silme/anonimleştirme bu görevde; iş kayıtları PII silme V15-KVK-001 kapsamında; V0-CMP-003
  disposal matrisi üstündür.

## Out of scope

- Base encryption/redaction, customer anonymization workflow ve secret rotation.

## Dependencies

- V0-CMP-003
- V1-OPS-001
- V15-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/Security/DataProtectionRetention/**` altında production code ve task-specific automated test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Expired payload, V0-CMP-003 disposal matrisine göre Anonymize edilir; yalnız matriste Delete sınıfındaki veriler
  silinir; re-encryption ve deletion retry idempotent'tır; plaintext/log leakage yoktur.

## Handoff

- V15-KVK-001
- V15-KVK-002
- V20-SEC-001

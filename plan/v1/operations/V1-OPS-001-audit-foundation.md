# V1-OPS-001 - Implement append-only audit foundation

- Task ID: V1-OPS-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.22
- PDF:II.9
- PDF:III.24

## Goal

Actor, reason, correlation ve before/after reference alanlarıyla V1 critical command'ları için immutable audit event
üretmek.

## Owned surface

- `src/Modules/Audit/EventStore/**`, `tests/Modules/Audit/EventStore/**`, `database/migrations/V1/V1-OPS-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- API, veritabanı yazma kısıtlamaları, hassas alan filtreleme ve temel V1 komut entegrasyonunu ekleyin.

## Out of scope

- Tamper-evident external archive ve KVKK anonymization execution.

## Dependencies

- V1-FND-001
- V1-IAM-002
- V0-CMP-003

## Deliverables

- `src/Modules/Audit/EventStore/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Sıradan uygulama rolleri denetim satırlarını güncelleyemez/silemez; reddedilen hassas alanlar hiçbir zaman yüklerden
  önce/sonra girilmez.

## Handoff

- V15-SEC-003
- V15-KVK-002

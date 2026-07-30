# V11-UNT-001 - Implement dimension-safe units and conversions

- Task ID: V11-UNT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.21-I.25
- PDF:II.2.10
- PDF:II.3.7
- PDF:III.12

## Goal

Boyutlar arası ve tutarsız döngüleri reddeden birim tanımlarını, boyutları ve deterministik dönüşümleri uygulayın.

## Owned surface

- `src/Modules/Recipes/Units/**`, `tests/Modules/Recipes/Units/**`, `database/migrations/V11/V11-UNT-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Birim kaydı, boyut kontrolleri, ters dönüştürme, kesinlik ve yuvarlama.

## Out of scope

- Tarif versiyonlama ve stok bakiyesi mutasyonu.

## Dependencies

- GATE-V11-ENTRY

## Deliverables

- `src/Modules/Recipes/Units/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- kg-g ve litre-ml dönüşümleri beyan edilen tolerans dahilinde geri döndürülebilir; kg-litre ve çelişkili çevrimler
  reddedilir.

## Handoff

- V11-RCP-001
- V11-PRD-002

# V14-QRS-001 - Implement QR token lifecycle

- Task ID: V14-QRS-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21

## Goal

Reusable raw secret saklamadan hashed, revocable ve time/policy-bound Table token yayımlamak.

## Owned surface

- `src/Modules/QrOrdering/TokenLifecycle/**`, `tests/Modules/QrOrdering/TokenLifecycle/**`,
  `database/migrations/V14/V14-QRS-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Token hash, issuance, rotation, expiry, revocation ve Table binding.

## Out of scope

- Toplu aktarma taşımacılığı, müşteri siparişi UI ve table status.

## Dependencies

- GATE-V14-ENTRY
- V0-QRG-001

## Deliverables

- `src/Modules/QrOrdering/TokenLifecycle/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Veritabanı sızıntısı, kullanılabilir ham belirtecin bulunmadığını ortaya çıkarıyor; süresi dolmuş/iptal edilmiş
  belirteç başarısız olur; döndürme, yapılandırıldığı şekilde önceki belirteci geçersiz kılar.

## Handoff

- V14-QRS-002
- V14-QRO-001

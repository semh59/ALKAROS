# V11-UI-002 - Implement production batch UI

- Task ID: V11-UI-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.22-I.23

## Goal

Tarif ve stok etkisi önizlemesi ile planned/start/complete/cancel production workflow'yi uygulayın.

## Owned surface

- `src/Clients/Cashier/Production/**`, `tests/Clients/Cashier/Production/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Parti durumu, planned/gerçek miktar, değişmez tarif ekranı, yetersiz stok ve kopya-tamamlama işlemleri.

## Out of scope

- Tarif düzenleme ve envanter ayarlaması.

## Dependencies

- V11-PRD-001
- V11-PRD-002
- V11-UI-001
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/Production/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Duplicate complete tek output üretir; referenced RecipeVersion read-only kalır; failure, ProductionBatch'i recoverable
  ve eksik stoku açıklar.
- Production ekranı `V0-CMP-005` kararındaki operations UI criteria ve approved exception kayıtlarını karşılar.

## Handoff

- V11-UI-003

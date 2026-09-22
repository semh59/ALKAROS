# V13-ALC-003 - Implement refund intents

- Task ID: V13-ALC-003
- Status: Done
- Assignee: Codex
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.6
- PDF:II.3.4-II.3.5
- PDF:II.5.3
- PDF:III.8

## Goal

Full veya partial refund talebinin eligibility, target allocation, amount ve idempotency değerlerini RefundIntent olarak
kalıcılaştırmak.

## Owned surface

- `src/Modules/Payments/Allocations/RefundIntents/**`, `tests/Modules/Payments/Allocations/RefundIntents/**`,
  `database/migrations/V13/V13-ALC-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
  — yalnız yeni "126" (refund_intents, phase B) girişi eklendi; mevcut
  hiçbir giriş değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
  (çok sayıda geçmiş migration görevinin sahipliğinde kalır — bu dosyanın
  kendi yorumu, PhaseBMax'ı her yeni pozisyonla aynı diff'te güncellemeyi
  zorunlu kılıyor) — yalnız `PhaseBMax` "125"→"126" ve üstündeki yorum
  güncellendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx — yalnız yeni
  `ALKAROS.Payments.Allocations.RefundIntents.Tests.csproj` girişi eklendi.

## In scope

- Allocation target, requested amount, cumulative eligibility snapshot, idempotency ve Pending/Rejected intent geçişi.

## Out of scope

- Provider transport, compensating allocation, net-paid mutation, fiscal refund ve inventory return.

## Dependencies

- V13-ALC-001
- V13-ALC-002
- V0-DOM-003
- V11-RSV-003

## Deliverables

- `src/Modules/Payments/Allocations/RefundIntents/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- 100 payment için 20 talep tek Pending RefundIntent üretir; duplicate aynı intent'i döndürür ve 100 üzeri talep
  provider çağrısından önce reddedilir.
- Bu görev PaymentAllocation veya net-paid amount değiştirmez.
- **2026-09-22 sonradan düzeltme** (bağımsız denetim, `tools/consistency-audit`'in
  `MODULE_SCHEMA` kör noktası — `Payments` bu sözlükte hiç yoktu, kapatılınca
  ortaya çıktı): `PostgresRefundIntentRepository.GetByAllocationIdAsync`'in
  LIMIT'siz bir SELECT çalıştırdığı bulundu — diğer store'ların "büyüyen bir
  tabloda sessizce sınırsız yük yerine yüksek sesle başarısız ol"
  konvansiyonuna uymuyordu. `LIMIT 500` eklendi (tek bir
  `payment_allocation_id`'nin gerçekçi iade sayısının çok üzerinde bir
  güvenlik sınırı). 11/11 test yeşil, davranış değişmedi.

## Handoff

- V13-HUG-003
- V13-ALC-004

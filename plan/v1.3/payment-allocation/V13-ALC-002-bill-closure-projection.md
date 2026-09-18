# V13-ALC-002 - Implement Bill allocation and payment-satisfied projections

- Task ID: V13-ALC-002
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

Allocated, paid ve change total değerlerini hesaplamak ve PaymentSatisfied projection'ını authoritative Payment
kayıtlarından atomik üretmek.

## Owned surface

- `src/Modules/Billing/PaymentClosure/**`, `tests/Modules/Billing/PaymentClosure/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/ALKAROS.Billing.csproj
  (V1-BIL-001 ailesinde kalır) — yalnız `PaymentClosure/**/*.cs`'i bu
  projenin derlemesinden hariç tutan bir `<Compile Remove>` eklendi.
  Gerekçe: `ALKAROS.Payments.csproj` zaten `ALKAROS.Billing.csproj`'a
  referans veriyor; bu görevin kodu Payment/PaymentAllocation tiplerine
  ihtiyaç duyduğu için `PaymentClosure/**`'ı kendi ayrı projesi
  (`ALKAROS.Billing.PaymentClosure.csproj`) yaptım — aksi halde
  Billing→Payments referansı döngü oluştururdu.
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx — yalnız yeni
  `ALKAROS.Billing.PaymentClosure.csproj` ve
  `ALKAROS.Billing.PaymentClosure.Tests.csproj` girişleri eklendi.

## In scope

- Projeksiyon formülleri, rebuild, kısmen tahsis edilmiş/ücretli durumlar ve kapatma engelleyicileri.

## Out of scope

- Ödemeler oluşturma, final Bill close status, fiscal gate ve geri ödeme provider çağrıları.

## Dependencies

- V13-ALC-001
- V1-BIL-001
- V0-DAT-004
- V1-FND-005

## Deliverables

- `src/Modules/Billing/PaymentClosure/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Rebuild projection deterministiktir; Pending/Unknown/Declined/Cancelled payment, PaymentSatisfied üretemez.
- Kesin allocation toplamı PaymentSatisfied değerini bir kez üretir; `V13-FSC-002` kararı olmadan Bill final closed
  status'e geçmez.

## Handoff

- V13-FSC-002
- V13-REC-001

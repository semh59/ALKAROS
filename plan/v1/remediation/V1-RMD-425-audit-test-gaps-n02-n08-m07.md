# V1-RMD-425 - Denetimlerde hayatta kalan mutasyonlar için eksik testler (N02, N08, M07)

- Task ID: V1-RMD-425
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

İki denetimin mutasyon turlarında üç koruma bozulduğu hâlde hiçbir test kırmızıya dönmemişti:

- V1-RMD-398 N02: stok bakiyesinin korumalı azaltmasında (`TryApplyGuardedOnHandDeltaAsync`) `>= 0` koşulunu kaldırmak.
  Tablo düzeyindeki CHECK kısıtı son savunma olarak aynı `null` sonucunu verdiği için fark görünmüyordu; ancak CHECK
  ihlali çağıranın işlemini bozar (sonraki her komut başarısız olur), koşul ise bozmaz.
- V1-RMD-398 N08: hareketli ortalama maliyette maliyet tarihindeki teslimleri dışlamak.
- V1-RMD-393 M07: tahsilatı tamamlanmış ama çözülmemiş (`Unknown`) bir kart ödemesi olan hesabı kapatmak.

Bu görev yalnız test ekler ve her mutasyonun artık yakalandığını kopyada mutasyonu uygulayarak gösterir. Üretim kodu
değişmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-425-audit-test-gaps-n02-n08-m07.md`
- `evidence/V1-RMD-425/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/BalanceProjection/StockBalanceDatabaseTests.cs
  (V11-INV-002 sahipliğinde) — N02 testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/PaymentClosure/BillPaymentClosureProjectorTests.cs
  (V13-ALC-002 sahipliğinde) — M07 testi

## In scope

- N02 ve M07 için yeni testler; N08 için V1-RMD-423'te eklenen testin mutasyonu yakaladığının gösterilmesi.

## Out of scope

- Üretim kodu.

## Dependencies

- V1-RMD-424

## Acceptance evidence

- `ALKAROS.Inventory.BalanceProjection.Tests` 14/14 ve `ALKAROS.Billing.PaymentClosure.Tests` 15/15 (gerçek
  PostgreSQL 18, Release, 0 uyarı / 0 hata; `evidence/V1-RMD-425/tests.log`).
- Yeni testler: `ARefusedGuardedDecreaseLeavesTheCallersTransactionUsable` (5 kg bakiyeden 6 kg azaltma aynı işlem
  içinde reddedilir; ardından 2 kg azaltma uygulanır ve işlem tamamlanır, bakiye 3) ve
  `ACoveredBillWithAnUnresolvedCardAttemptIsNotClosed` (60 TL tahsil edilmiş, ayrıca `Unknown` bir kart denemesi
  olan hesap kapatılmaz, engel listesi dolu).
- Mutasyonlar kopyada tek tek uygulandı ve üçü de yakalandı (`evidence/V1-RMD-425/mutations.log`): N02 (koşulu
  `WHERE true` yapmak), N08 (`<=` yerine `<`; V1-RMD-423'ün yerel tarih testi yakalıyor; denetimdeki mutasyon metni
  V1-RMD-423'ten sonraki koda göre uyarlandı) ve M07 (engelleri yok saymak).
- Semih'in elle deneyebileceği senaryo: yok; yalnız test eklendi, davranış değişmedi.

## Handoff

- None

# V1-RMD-414 - Hesabı masaya geri almada para kontrolü ile hesap iptalini tek işlemde yapmak

- Task ID: V1-RMD-414
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-393 para akışı denetimi F-16 (Düşük): kasaya gönderilmiş hesabı masaya geri alma (`recall-from-cashier`)
hesapta para hareketi olmadığını bir işlemde kontrol ediyor, hesabı ise o işlem bittikten sonra ayrı bir kayıtla iptal
ediyordu. İkisinin arasına giren bir tahsilat, iptal edilmiş bir hesaba bağlı kalabiliyordu.

Bu görev: geri alma, siparişin hesapları için bütün ödeme yollarının aldığı hesap başına kilidi
(`bill-settlement:{billId}`) işlemin başında alır; para kontrolü, masaya bağlama ve açık hesapların iptali aynı işlemde
yapılır. Kilidi bekleyen bir tahsilat, kilit bırakıldığında hesabı iptal edilmiş bulur ve reddedilir (V1-RMD-409).
Hesap iptali Billing modülünün deposu üzerinden, dışarıdan verilen bağlantı ve işlemle yazılır (masa birleştirmedeki
desen).

## Owned surface

- `plan/v1/remediation/V1-RMD-414-recall-money-check-atomic.md`
- `evidence/V1-RMD-414/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/CashierHandoffStore.cs (V1-WTR-033
  sahipliğinde) — yalnız geri almadaki kilit, para kontrolü ve iptalin aynı işleme alınması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-RMD-101
  sahipliğinde) — yalnız geri alma ucunun ayrı iptal döngüsünün kaldırılması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/BillFoundation/IBillRepository.cs ve
  src/Modules/Billing/BillFoundation/PostgresBillRepository.cs (V1-RMD-002 sahipliğinde) — yalnız işlem içinde
  siparişin açık hesaplarını iptal eden yeni metot
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs
  (V1-RMD-083 sahipliğinde) — yeni test

## In scope

- Geri almada `bill-settlement` kilitleri, para kontrolü, masaya bağlama ve hesap iptali tek işlemde.
- Tekrarlanan istek (`AlreadyAttached`): açık hesaplar yalnız para hareketi yoksa ve aynı kilit altında iptal edilir.

## Out of scope

- Geri almanın izin ve Türkçe hata davranışı (değişmez).

## Dependencies

- V1-RMD-413

## Acceptance evidence

- `ALKAROS.Host.Experience.Orders.TableDraft.Tests` (gerçek PostgreSQL 18, Release, 0 uyarı / 0 hata): 87/87
  (`evidence/V1-RMD-414/tests.log`). Geri almanın mevcut testleri (yanlışlıkla gönderilen hesap geri döner ve hesabı
  iptal edilir, tekrar istek `AlreadyAttached`, paralı hesap 409) değişmeden geçer.
- Yeni test `ATenderThatHoldsTheBillLockWhenARecallStartsKeepsTheCheckAtTheTill`: bir tahsilat hesabın
  `bill-settlement` kilidini tutarken geri alma başlar; tahsilat ödemeyi yazıp işlemini bitirir; geri alma 409
  `CHECK_HAS_PAYMENT` döner, hesap `Open` kalır, masa hesaba bağlanmaz. Üretim değişikliği geri alınınca kırmızı
  (409 beklenirken 200: geri alma kilidi beklemeden parasız görüp hesabı iptal ediyordu;
  `evidence/V1-RMD-414/red-without-fix.log`).
- Semih'in elle deneyebileceği senaryo: kasada ödeme alınırken aynı anda garson hesabı masaya geri almaya çalışırsa
  "Bu hesapta tahsilat başlamış; masaya geri alınamaz." görür; ödeme iptal edilmiş bir hesapta kalmaz.

## Handoff

- None

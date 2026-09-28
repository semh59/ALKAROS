# V1-RMD-409 - İptal edilmiş hesaba tahsilatı ve çözülmemiş kart varken nakdi reddetmek

- Task ID: V1-RMD-409
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-393 para akışı denetiminin iki Yüksek bulgusu:

- F-07: iptal edilmiş (`Cancelled`) bir hesaba nakit tahsilat kabul ediliyor. Üretimde hesabı kasadan geri çağırma
  (recall) hesabı iptal eder; ekranında eski hesap açık kalan kasiyer ödeme alabiliyor; para ölü hesapta kalıyor ve
  yeniden gönderilen hesap yeni ödeme istiyor.
- F-04: aynı hesapta çözülmemiş bir kart denemesi (`Unknown`/`Pending`/`ReconciliationRequired`) varken nakit tahsilat
  kabul ediliyor; EFT ve kart bu durumu reddediyor. Kart gerçekte çekildiyse müşteri iki kez ödemiş olur. V1-RMD-258'in
  nakdi dışarıda bırakma gerekçesi ("nakit kendisi Unknown olmaz") başka bir ödemenin Unknown olmasını kapsamıyor.

Bu görev: ödeme dağıtımı (bütün tahsilat yöntemlerinin ortak geçtiği `AllocateAsync`) hesap kilidi altında hesabın
güncel durumunu okur ve `Cancelled` hesaba dağıtımı reddeder; kart denemesi de (dağıtım yapmadan `Unknown` kalabildiği
için) iptal edilmiş hesabı reddeder; nakit tahsilat EFT ve kartla aynı paylaşılan hesap kilidini alır ve hesapta
çözülmemiş bir ödeme varken reddedilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-409-no-tender-on-cancelled-bill-or-unsettled-card.md`
- `evidence/V1-RMD-409/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/Allocations/Persistence/PostgresPaymentAllocationRepository.cs
  ve src/Modules/Payments/Allocations/Persistence/PaymentAllocationExceptions.cs (V13-ALC-001 sahipliğinde) — yalnız
  kilit altındaki hesap durumu kontrolü ve yeni istisna
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/CardSettlement/CardSettlementOrchestrator.cs
  (V13-PAY-004 sahipliğinde) — yalnız iptal edilmiş hesap kontrolü
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/TenderHandler/CashTenderHandler.cs ve
  src/Modules/Cash/TenderHandler/CashTenderExceptions.cs (V13-CSH-003 sahipliğinde) — yalnız paylaşılan hesap kilidi
  ve çözülmemiş ödeme kontrolü
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs (V13-PUI-001
  sahipliğinde) ve src/Host/DualScreen/DualScreenApplication.cs (V1-IAM-024 sahipliğinde) — yalnız iki yeni istisnanın
  Türkçe 409 eşlemesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Cash/TenderHandler/CashTenderHandlerTests.cs (V13-CSH-003
  sahipliğinde), tests/Host/Experience/CashSession/CashSessionHttpTests.cs (V13-CSH-004 sahipliğinde) ve
  tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs (V13-PUI-001 sahipliğinde) — yeni testler

## In scope

- `Cancelled` hesaba nakit, EFT ya da kart tahsilatı: 409 `TENDER_BILL_NOT_PAYABLE`, hiçbir ödeme/dağıtım/kasa
  kaydı yazılmaz.
- Çözülmemiş ödemesi olan hesaba nakit tahsilat: 409 `TENDER_UNSETTLED_PAYMENT_EXISTS`, hiçbir kayıt yazılmaz.
- Aynı idempotency anahtarıyla tekrar (replay) davranışı değişmez.

## Out of scope

- Geri çağırma akışının para kontrolü ile iptali aynı işlemde yapması (V1-RMD-393 F-16) — ayrı görev.
- Çözülmemiş kart "çekilmedi" diye çözülünce hesabın kapanması (F-06) — ayrı görev.

## Dependencies

- V1-RMD-408

## Acceptance evidence

- Görev kapanışında bu bölüm gerçek koşu çıktılarıyla doldurulur.

## Handoff

- None

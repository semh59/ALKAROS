# V1-RMD-409 - İptal edilmiş hesaba tahsilatı ve çözülmemiş kart varken nakdi reddetmek

- Task ID: V1-RMD-409
- Status: Done
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

- Gerçek PostgreSQL 18, Release, 0 uyarı / 0 hata (`evidence/V1-RMD-409/tests.log`): `ALKAROS.Cash.TenderHandler.Tests`
  7/7, `ALKAROS.Payments.Allocations.Persistence.Tests` 15/15, `ALKAROS.Payments.CardSettlement.Tests` 8/8,
  `ALKAROS.Payments.EftTender.Tests` 12/12, `ALKAROS.Host.Experience.CashSession.Tests` 19/19,
  `ALKAROS.Host.Experience.PaymentTender.Tests` 33/33.
- Yeni testler: `ACashTenderOnACancelledBillIsRefusedAndRecordsNothing` (409 `TENDER_BILL_NOT_PAYABLE`, ödeme yok,
  beklenen kasa açılış bakiyesinde kalır), `ACashTenderIsRefusedWhileACardAttemptOnTheSameBillIsUnresolved` (gerçek
  kart ucu `RequiresReconciliation` bırakır; ardından nakit 409 `TENDER_UNSETTLED_PAYMENT_EXISTS`, yalnız kart ödemesi
  kalır), `EftAndCardTendersOnACancelledBillAreRefusedAndRecordNothing` (EFT ve kart 409, ödeme yok). Üçü de üretim
  değişikliği geri alınınca kırmızı (`evidence/V1-RMD-409/red-without-fix.log`).
- V1-RMD-393 denetim probe'ları P01 (F-04) ve P03 (F-07) düzeltilmiş kopyada geçer
  (`evidence/V1-RMD-409/money-flow-probes-after-fix.log`; harness'e yalnız kopyada bugünkü kasiyer izinleri eklendi).
- Semih'in elle deneyebileceği senaryo: garson hesabı kasadan geri çağırıp yeniden gönderdiğinde, kasadaki eski ekrandan
  nakit alınmak istenince "Bu hesap iptal edilmiş; tahsilat alınamaz. Hesabı yenileyin." görülür. Kart denemesi sonuçsuz
  kalmış bir hesaba nakit alınmak istenince, önce kart ödemesinin sonucunu netleştirmek gerektiği söylenir.

## Handoff

- None

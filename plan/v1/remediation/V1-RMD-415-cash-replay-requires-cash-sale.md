# V1-RMD-415 - Nakit tahsilatta işlem kimliği tekrarının yalnız aynı nakit satışı için geçerli olması

- Task ID: V1-RMD-415
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-393 para akışı denetimi F-12 (Düşük): EFT tahsilatında kullanılmış bir işlem kimliğiyle (idempotency key) gelen
nakit tahsilat, "aynı isteğin tekrarı" sayılıp 200 dönüyordu; oysa kasaya hiçbir nakit satış kaydı yazılmamıştı.
Kasiyer parayı çekmeceye koyar, gün sonunda kasa fazla çıkar. Tekrar kontrolü yalnız anahtarın varlığına bakıyor;
kaydın bu hesaba, bu tutara ve bu kasa oturumundaki bir nakit satışa ait olduğunu doğrulamıyordu.

Bu görev: nakit tahsilatın tekrar yolu, mevcut dağıtımın aynı hesaba ve aynı tutara ait olduğunu ve o ödemeye bağlı
nakit satış kaydının bu kasa oturumunda bulunduğunu doğrular; değilse Türkçe 409 ile reddeder, hiçbir kayıt yazılmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-415-cash-replay-requires-cash-sale.md`
- `evidence/V1-RMD-415/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/TenderHandler/CashTenderHandler.cs ve
  src/Modules/Cash/TenderHandler/CashTenderExceptions.cs (V13-CSH-003 sahipliğinde) — yalnız tekrar doğrulaması ve
  yeni istisna
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs (V1-IAM-024 sahipliğinde) —
  yalnız yeni istisnanın Türkçe 409 eşlemesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Cash/TenderHandler/CashTenderHandlerTests.cs
  (V13-CSH-003 sahipliğinde) — yeni testler

## In scope

- Başka yöntemle (EFT, kart) kullanılmış anahtar, başka hesap ya da başka tutarla gelen nakit tahsilat → 409
  `TENDER_IDEMPOTENCY_KEY_REUSED`.
- Aynı nakit isteğinin gerçek tekrarı eskisi gibi aynı sonucu döner.

## Out of scope

- EFT tahsilatının nakitte kullanılmış anahtarla tekrarı: kasaya etkisi yok; Payments modülü kasa defterini
  okumadığı için ayrı karar.

## Dependencies

- V1-RMD-414

## Acceptance evidence

- `ALKAROS.Cash.TenderHandler.Tests` 9/9 ve `ALKAROS.Host.Experience.CashSession.Tests` 20/20 (gerçek PostgreSQL 18,
  Release, 0 uyarı / 0 hata; `evidence/V1-RMD-415/tests.log`). Aynı isteğin gerçek tekrarı ve eşzamanlı iki aynı
  istek testleri değişmeden geçer.
- Yeni testler: `ACashTenderReusingAKeyAnotherTenderMethodUsedIsRefusedAndRecordsNothing` (EFT'nin anahtarıyla nakit
  → reddedilir, kasa defteri boş, ödeme sayısı değişmez) ve `ACashKeyReusedForAnotherAmountIsRefusedNotReplayed`
  (aynı anahtar başka tutarla → reddedilir, tek satış kaydı). Üretim değişikliği geri alınınca ikisi de kırmızı
  (istisna beklenirken "tekrar" dönüyordu; `evidence/V1-RMD-415/red-without-fix.log`).
- V1-RMD-393 probe'u P08 düzeltilmiş kopyada geçer (`evidence/V1-RMD-415/money-flow-probes-after-fix.log`). Aynı
  koşuda P06 (F-10) açık bulgudur; P07 ve P09 ön koşulları V1-RMD-409'dan beri erişilemez.
- Semih'in elle deneyebileceği senaryo: yalnız elle hazırlanmış istekle erişilir (kasa ekranı yöntem ya da tutar
  değişince yeni işlem kimliği üretir); böyle bir istek artık "İşlem kimliği başka bir tahsilat için zaten
  kullanılmış." uyarısıyla reddedilir.

## Handoff

- None

# V1-RMD-417 - Kasa kapanışı ve sayımın yalnız sayım aşamasında yapılabilmesi

- Task ID: V1-RMD-417
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-393 para akışı denetimi F-15 (Düşük): kasa oturumu tasarımı (`docs/domain/cash-session-design.md` §5) sayımın
yalnız `Counting` durumunda, kapanışın yalnız `Counting` ya da `Closing` durumunda yapılmasını söylüyor. Politika
(`CashSessionPolicy`) ikisine de `Open` durumda izin veriyordu: sayım başlatılmadan (satışlar kilitlenmeden) kasa
kapatılabiliyor ya da sayım kaydedilebiliyordu. Kasa ekranı zaten "sayımı başlat → say → kapat" sırasını izliyor; bu
sapmaya yalnız doğrudan istekle ulaşılıyordu.

Bu görev: politika sayımı yalnız `Counting`, kapanışı yalnız `Counting` ya da `Closing` durumunda kabul eder; `Open`
durumdaki istek mevcut Türkçe 409 `INVALID_CASH_SESSION_STATE` ile reddedilir. Doğrudan `Open`'dan kapatan testler
sayımı başlatacak şekilde güncellenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-417-cash-close-requires-count.md`
- `evidence/V1-RMD-417/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/Contracts/CashSessionPolicy.cs ve
  tests/Modules/Cash/Contracts/CashSessionPolicyTests.cs (V1-CSH-001 sahipliğinde) — yalnız iki durum kontrolü ve
  testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Cash/SessionLifecycle/CashSessionLifecycleServiceTests.cs
  (V13-CSH-001 sahipliğinde), tests/Modules/Cash/TenderHandler/CashTenderHandlerTests.cs (V13-CSH-003 sahipliğinde) ve
  tests/Host/Experience/CashSession/CashSessionHttpTests.cs (V13-CSH-004 sahipliğinde) — yalnız kapanıştan önce sayımı
  başlatma ve yeni test

## In scope

- `RecordCashCount`: yalnız `Counting`.
- `CloseSession`: yalnız `Counting` ya da `Closing`.

## Out of scope

- Kasa ekranı (akışı zaten uyumlu).
- V1-RMD-393 denetim probe'ları (kapanmış görevin kanıtı; P10 `Open`'dan kapatıyor, sonuç kapanışta belgelenir).

## Dependencies

- V1-RMD-416

## Acceptance evidence

- `ALKAROS.Cash.Contracts.Tests` 14/14, `ALKAROS.Cash.SessionLifecycle.Tests` 12/12, `ALKAROS.Cash.TenderHandler.Tests`
  9/9 ve `ALKAROS.Host.Experience.CashSession.Tests` 22/22 (gerçek PostgreSQL 18, Release, 0 uyarı / 0 hata;
  `evidence/V1-RMD-417/tests.log`). `Open`'dan doğrudan kapatan 13 test adımı önce sayımı başlatacak şekilde güncellendi;
  beklenen tutarları değişmedi.
- Yeni testler: `AnOpenSessionCanNeitherRecordACountNorCloseBeforeCountingStarts` (politika) ve
  `ADrawerCannotBeCountedOrClosedBeforeCountingStarts` (HTTP: `Open` durumda sayım ve kapanış 409
  `INVALID_CASH_SESSION_STATE`, oturum `Open` kalır; sayım başlatılınca kapanış 200). Üretim değişikliği geri alınınca
  ikisi de kırmızı (`evidence/V1-RMD-417/red-without-fix.log`).
- V1-RMD-393 probe'u P05'in ön koşulu kasayı `Open`'dan kapatıyordu ve artık 409 alıyor; probe dosyası kapanmış
  görevin kanıtı olduğu için değiştirilmedi. Kopyada kapanıştan önce sayım başlatılınca P05 geçer (kasiyer kendi
  oturumunu uzlaştıramaz); aynı kural `ReconcileNeedsASupervisorWhoIsNotTheSessionsOwnCashier` HTTP testiyle de
  korunuyor (`evidence/V1-RMD-417/money-flow-probes-after-fix.log`). Aynı koşuda P06 (F-10) açık bulgudur; P07 ve P09
  ön koşulları V1-RMD-409'dan beri erişilemez.
- Semih'in elle deneyebileceği senaryo: kasa ekranında akış aynı (sayımı başlat → say → kapat); sayım başlatılmadan
  kasa kapatma isteği artık "Kasa oturumu bu işlem için uygun durumda değil." ile reddedilir.

## Handoff

- None

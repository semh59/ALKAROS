# V1-RMD-416 - Kasa açılışında oturum ve açılış defter kaydını tek işlemde yazmak

- Task ID: V1-RMD-416
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-393 para akışı denetimi F-14 (Düşük): kasa açılışında oturum kaydı ile açılış tutarının (`Opening`) kasa
defterine yazılması iki ayrı yazmaydı. İkinci yazma düşerse oturum açık kalır ama defterde açılış tutarı yoktur;
kapanışta beklenen kasa 0'dan başlar ve kasa, konulan açılış tutarı kadar fazla görünür.

Bu görev: kasa oturumu yaşam döngüsü servisine, oturumu ve açılış defter kaydını aynı veritabanı işleminde yazan bir
açılış yolu eklenir; kasa açılış ucu bu yolu kullanır. Defter kaydı yazılamazsa oturum da açılmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-416-cash-session-opening-entry-atomic.md`
- `evidence/V1-RMD-416/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/SessionLifecycle/ICashSessionLifecycleService.cs,
  src/Modules/Cash/SessionLifecycle/CashSessionLifecycleService.cs,
  src/Modules/Cash/SessionLifecycle/ICashSessionRepository.cs ve
  src/Modules/Cash/SessionLifecycle/PostgresCashSessionRepository.cs (V13-CSH-001 sahipliğinde) — yalnız açılış
  kaydıyla birlikte atomik açılış yolu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.CashSession.cs (V13-CSH-004
  sahipliğinde) — yalnız açılış ucunun yeni yola geçmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/CashSession/CashSessionHttpTests.cs (V13-CSH-004
  sahipliğinde) — yeni test

## In scope

- Açılış tutarı > 0 olan kasa açılışında oturum ve `Opening` defter kaydı tek işlem.
- Defter yazması başarısızsa oturum oluşmaz.

## Out of scope

- Mevcut `OpenSessionAsync` davranışı (açılış kaydı yazmaz; modül testleri ve diğer kullanıcıları değişmez).

## Dependencies

- V1-RMD-415

## Acceptance evidence

- `ALKAROS.Host.Experience.CashSession.Tests` 21/21, `ALKAROS.Cash.SessionLifecycle.Tests` 12/12 ve
  `ALKAROS.Cash.TenderHandler.Tests` 9/9 (gerçek PostgreSQL 18, Release, 0 uyarı / 0 hata;
  `evidence/V1-RMD-416/tests.log`). Açılış kaydının yazıldığını ve kapanışın ondan hesaplandığını sınayan mevcut testler
  değişmeden geçer.
- Yeni test `AFailedOpeningLedgerWriteLeavesNoSessionBehind`: `Opening` defter yazması veritabanında zorla
  başarısız kılınınca açılış başarısız döner ve terminalde oturum kalmaz; engel kaldırılınca aynı terminalde açılış 201
  döner. Üretim değişikliği geri alınınca kırmızı (oturum sayısı 0 beklenirken 1;
  `evidence/V1-RMD-416/red-without-fix.log`).
- Semih'in elle deneyebileceği senaryo: normal kullanımda görünür bir fark yok; kasa açılışında bir veritabanı hatası
  olursa kasa açılmamış sayılır ve yeniden açılabilir, açılış tutarı kaybolmuş bir oturum kalmaz.

## Handoff

- None

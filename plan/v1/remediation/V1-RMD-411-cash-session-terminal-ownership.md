# V1-RMD-411 - Kasa oturumuna yalnız kendi terminalinden erişilmesi

- Task ID: V1-RMD-411
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-393 para akışı denetimi F-08 (Orta): B terminalinde oturum açmış bir kasiyer, A terminalinin kasa oturumuna
satış yazabiliyor; kasa oturumu kimliği rotada verildiğinde oturumun o terminale ait olduğu kontrol edilmiyor. Aynı açık
sayım, kapatma, mutabakat, para giriş/çıkışı ve beklenen nakit uçlarında da var. Bu görev: kasa oturumu kimliği taşıyan
her uç, oturumun rotadaki terminale ait olduğunu doğrular; başka terminalin oturumu bilinmeyen oturumla aynı yanıtı
alır (404 `CASH_SESSION_NOT_FOUND`).

## Owned surface

- `plan/v1/remediation/V1-RMD-411-cash-session-terminal-ownership.md`
- `evidence/V1-RMD-411/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.CashSession.cs (V13-CSH-004
  sahipliğinde) — yalnız yedi uçtaki terminal sahipliği kontrolü
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/CashSession/CashSessionHttpTests.cs (V13-CSH-004
  sahipliğinde) — terminaller arası erişim testi

## In scope

- `start-count`, `counts`, `expected-cash`, `close`, `reconcile`, `cash-movements`, `cash-tender`: oturum başka
  terminalinse 404; hiçbir kayıt yazılmaz.

## Out of scope

- Terminal kaydı/eşleştirmesi (terminal kimliğinin kendisinin doğrulanması).

## Dependencies

- V1-RMD-410

## Acceptance evidence

- `ALKAROS.Host.Experience.CashSession.Tests` (gerçek PostgreSQL 18, Release, 0 uyarı / 0 hata): 20/20
  (`evidence/V1-RMD-411/tests.log`).
- Yeni test `ACashierOnAnotherTerminalCannotTouchThisTerminalsDrawerSession`: B terminalindeki kasiyerin A'nın
  oturumuna yedi ucun hepsinden istekleri 404; A'nın beklenen nakdi 100'de, oturumu `Open` kalır. Üretim değişikliği geri
  alınınca kırmızı (aynı dosya).
- V1-RMD-393 denetim probe'u P04 (F-08) düzeltilmiş kopyada geçer (aynı dosya).
- Semih'in elle deneyebileceği senaryo: iki kasa açıkken bir kasadaki kasiyer diğer kasanın oturumuna (kimliğini
  bilse bile) satış ya da para çıkışı yazamaz; kasa oturumu bulunamadı yanıtı alır.

## Handoff

- None

# V1-RMD-411 - Kasa oturumuna yalnız kendi terminalinden erişilmesi

- Task ID: V1-RMD-411
- Status: InProgress
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

- Görev kapanışında bu bölüm gerçek koşu çıktılarıyla doldurulur.

## Handoff

- None

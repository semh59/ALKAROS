# V1-RMD-291 - PosTerminal ve Kasa sayfalarında oturum düşmesi tutarlı işlenir

- Task ID: V1-RMD-291
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

Oturum sunucuda düştüğünde istemciler farklı davranıyor: garson PWA `api.js` 401'de giriş katmanını açıyor; PosTerminal `api.ts` 401 için genel bir işleyiciye sahip değil, yalnız `Cashier.tsx` bazı çağrılarda oturumu `anonymous` yapıyor, `pendingChecksApi.ts` yalnız 'Oturum sona erdi.' yazıp giriş ekranına dönmüyor, birçok çalışma alanı kendi 'yetkisiz' durumunu gösteriyor. Statik Kasa sayfaları (`split-payment.js`, `cash-session.js`) 401'de yalnız bir ileti gösteriyor. Sonuç: oturum düşünce kasiyer yarım kalmış bir ekranda tıkanır. Bu görev PosTerminal `request` katmanında tek bir 401 işleyicisi (oturumu düşür, giriş ekranına dön, yarım işlemi koru) ve statik sayfalarda aynı davranışı ekler.

## Owned surface

- `plan/v1/remediation/V1-RMD-291-posterminal-session-expiry-handling.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/api.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx
  (yalnız oturum durumu)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js

## In scope

1. Tek 401 işleyicisi ve oturum durumu, yeniden giriş sonrası ekrana dönüş.
2. Statik Kasa sayfalarında aynı davranış.
3. Testler (vitest ve statik istemci testleri).

## Out of scope

- Sunucu oturum süresi politikası.

## Dependencies

- V13-RMD-002
- V1-RMD-280

## Acceptance evidence

- vitest: herhangi bir çağrıda 401 oturumu düşürür ve giriş ekranı açılır; statik istemci testi: split-payment ve cash-session 401'de giriş aşamasına geçer.
- Kasa E2E: oturum sunucuda iptal edilince ekran giriş aşamasına döner.
- `plan_audit_tool.py validate` temiz.

## Handoff

- None

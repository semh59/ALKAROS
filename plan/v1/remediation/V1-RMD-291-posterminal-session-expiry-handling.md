# V1-RMD-291 - PosTerminal ve Kasa sayfalarında oturum düşmesi tutarlı işlenir

- Task ID: V1-RMD-291
- Status: Done
- Assignee: Claude Sonnet 5
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
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/15-session-expiry.spec.js

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

- PosTerminal vitest: 27 dosya, 205/205. `pnpm typecheck` temiz.
- Cashier E2E (gerçek Host + Chromium): 36/36 (34 mevcut + 2 yeni `15-session-expiry`). Yeni: Hesap Ödeme'de tahsilat sırasında sunucu 401 dönünce ekran 'Kasa Girişi' fazına döner ve '/' adresine giden bir bağlantı gösterir; Kasa Oturumu'nda vardiya açılışı sırasında 401 dönünce sayfanın kendi gerçek giriş formu (kullanıcı adı/şifre) görünür.
- Mutasyon kontrolü: `api.ts`, `Cashier.tsx`, `split-payment.js`, `cash-session.js` eski hâline döndürülünce yeni 2 E2E senaryosu da kırıldı.
- `plan_audit_tool.py validate` ve `consistency_audit.py` temiz.

### Kapsam sapması (dürüstçe kaydedildi)

Görevin metni `pendingChecksApi.ts` ve PosTerminal `workspace.tsx`'teki 6 ayrı 'yetkisiz' durumunu da örnek veriyordu, ama görevin Owned surface'ı bu iki dosyayı LİSTELEMİYOR (yalnız `api.ts`, `Cashier.tsx` oturum durumu, ve iki statik sayfa). Bu yüzden dokunulmadı:

- `pendingChecksApi.ts` kendi `fetcher`'ını kullanıyor, `api.ts`'in `request()` katmanından geçmiyor; yeni tek işleyici bunu kapsamıyor.
- `workspace.tsx`'teki 6 sekme (Tables, Billing, Catalog, SystemHealth, Authorization, Kitchen) kendi özellik istemcilerini kullanıyor, hiçbiri `api.ts` üzerinden geçmiyor; her biri hâlâ kendi yerel 'yetkisiz' metnini gösteriyor, oturumu düşürmüyor.
- Bu iki sınıf, bu görevin uyguladığı `api.ts` tek işleyicisiyle ÇÖZÜLMEDİ. Kapsam genişletmeden, ayrı bir görev (`V1-RMD-291` takibi) açılması önerilir: ya bu 6 istemciye ortak bir `onUnauthorized` geri çağırma eklenir, ya da hepsi `api.ts` üzerinden geçecek şekilde yeniden yazılır.

## Handoff

- None

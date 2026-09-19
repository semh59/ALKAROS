# V14-QNB-007 - QNB credential connection test

- Task ID: V14-QNB-007
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- EXT:QNB-API-PUBLIC
- PO:2026-09-18

## Goal

`V14-QNB-006` sadece credential'ları saklıyor, hiç QNB'ye dokunmuyordu.
Semih'in isteğiyle ("dış bağımlılık kod tarafında ve arayüzde bitsin"):
kaydedilen `userId`/`password` ile QNB'nin GERÇEK test sunucusuna
(`erpefaturatest1.qnbesolutions.com.tr/efatura/ws/userService`) bir
`wsLogin` denemesi yapıp sonucu (başarılı/başarısız + QNB'nin kendi
hata mesajı) arayüzde göstermek.

**Bu görev `V0-QNB-001`'e bağımlı DEĞİL** — `V13-HUG-001..004`'ün aksine,
bu özellik "e-Fatura gönderiminin tam olarak çalıştığını" iddia etmiyor,
yalnızca "girilen kullanıcı adı/şifre ile QNB'nin gerçek sunucusuna
bağlanılabiliyor mu" sorusuna gerçek bir cevap veriyor — ki bu, imzalı
bir private contract olmadan da, QNB'nin PUBLIC test endpoint'i canlı
olduğu için test edilebilir. Gerçek bir bağlantı bugün curl ile
doğrulandı (bkz. Acceptance evidence).

## Owned surface

- `src/Modules/Invoicing/Qnb/Client/**`
- `tests/Modules/Invoicing/Qnb/Client/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/QnbCredentialSettings/QnbCredentialSettingsEndpoints.cs
  (V14-QNB-006 sahipliğinde kalır) — yalnız yeni `POST .../qnb-credential/test-connection`
  endpoint'i eklenir, mevcut save/status davranışı değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/QnbCredentialSettings.tsx,
  QnbCredentialSettings.test.tsx (V14-QNB-006 sahipliğinde kalır) —
  yalnız "Bağlantıyı Test Et" düğmesi ve sonuç göstergesi eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/api.ts,
  contracts.ts (birikimli olarak birçok görevin sahipliğinde) — yalnız
  yeni `testQnbConnection` çağrısı eklenir.

## In scope

- `QnbSoapClient`: gerçek QNB dokümantasyonundan doğrulanmış SOAP zarf
  şemasına göre `wsLogin`/`logout` (bkz. `evidence/v0/integrations/
  V0-QNB-001/**` ve `evidence/V13-GOV-007/**`'nin taslağı — birebir aynı
  mantık, gerçek Owned surface'a taşınıyor).
- Gerçek QNB test URL'sine (`https://erpefaturatest1.qnbesolutions.com.tr/efatura/ws/userService`)
  karşı gerçek bir `wsLogin` denemesi; SOAP `Fault`'u (`faultcode`/
  `faultstring`) sanitize edilmiş bir Türkçe sonuca çevirme.
- `POST /api/v1/terminals/{terminalId}/qnb-credential/test-connection` —
  kayıtlı credential'ı okuyup gerçek denemeyi yapar, ham QNB hata
  metnini asla olduğu gibi kullanıcıya döndürmez (docs/UI_STYLE_GUIDE.md
  §3).
- `QnbCredentialSettings.tsx`'e "Bağlantıyı Test Et" düğmesi.

## Out of scope

- Fatura gönderimi/durum sorgusu/kayıtlı kullanıcı listesi — bunlar
  `V14-QNB-001/002/003`'ün kendi kapsamı, hâlâ `V0-QNB-001`'e bağımlı.
- WS-Security `UsernameToken` header'ı — kamuya açık örnekte zorunlu
  görünmüyor, gerçek denemede de gerekmedi.

## Dependencies

- None

## Deliverables

- Yukarıdaki Owned surface'ın production code + testleri.

## Acceptance evidence

- Gerçek bir curl denemesi (2026-09-18, bu görevden önce, aynı
  oturumda) `https://erpefaturatest1.qnbesolutions.com.tr/efatura/ws/userService`'e
  gerçek bir `wsLogin` SOAP zarfı gönderdi ve gerçek bir SOAP Fault aldı:
  `faultcode: EF0003`, `faultstring: "[EF0003] Oturum açma işlemi
  başarısız. ..."` — zarf şemasının QNB'nin gerçek sunucusu tarafından
  doğru ayrıştırıldığını kanıtlıyor.
- Birim testleri: sahte HTTP handler ile hem başarı hem `EF0003` fault
  senaryosu (yukarıdaki gerçek metinle) doğrulanır.
- HTTP testi: yetkisiz kullanıcı 403 alır; endpoint ham QNB hata metnini
  hiçbir zaman olduğu gibi döndürmez, yalnız `success`/Türkçe mesaj.
- `dotnet build`/`dotnet test` 0 hata; `pnpm typecheck` 0 hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- V14-QNB-001

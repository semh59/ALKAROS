# V0-YSP-001 - Validate Yemeksepeti Partner API contract

- Task ID: V0-YSP-001
- Status: Blocked
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.6
- PDF:I.6.4
- EXT:YSP-PARTNER-2.0.2

## Goal

Partner erişimi, webhook kimliği, retry, order status, cancellation ve catalog mapping sözleşmesini doğrulamak.

## Owned surface

- `evidence/v0/integrations/V0-YSP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- OAuth/client credentials, webhook security, event identity, status vocabulary, retry count ve market availability.

## Out of scope

- Internal Order mapping veya production webhook handler.
- Araştırma notu (2026-10-17, V1-RMD-584), kayıt durumunu ve kararı değiştirmez:
- Çelişki (2026-10-16/17): EXT:YSP-PARTNER-2.0.2 (developer.yemeksepeti.com, "Q-Commerce Integration Solutions") market/mağaza toplama akışı içindir. Restoran POS entegrasyonu Delivery Hero POS Middleware + Plugin API üzerinden görünüyor (https://developers.deliveryhero.com/documentation/pos.html ; https://integration-middleware.stg.restaurant-partners.com/apidocs/pluginApi.yaml ve pluginOrder.yaml): giriş `POST /v2/login`, gelen istekler HS512 JWT ile doğrulanır, yeni siparişe `remoteResponse.remoteOrderId` ile 200/202 dönülür, durumlar `order_accepted` / `order_rejected` / `order_picked_up`. Middleware belgesi "Yemeksepeti in Turkey" ve "Yemek" adlarını geçirir; ancak Delivery Hero'nun desteklenen platform listesinde Yemeksepeti adı geçmez ve integration.yemeksepeti.com okunamadı (HTTP 522). Yani hangi sözleşmenin geçerli olduğu Yemeksepeti'den **yazılı teyit** olmadan bilinmez.
- Erişim süreci (DH POS): NDA, açık PGP anahtarıyla "Get Credentials" formu, kimlik bilgilerinin PGP ile şifreli iletimi, geçerli SSL'li HTTPS eklenti adresi, test restoranı, pilot, yayın. Başvuru süresi bulunamadı.
- Karar bekliyor: kaynağın EXT:YSP-PARTNER-2.0.2 yerine DH POS Middleware/Plugin API olarak güncellenmesi ve mevcut Yemeksepeti HTTP kodunun "restoran için yanlış sözleşmeye göre yazılmış taslak" diye işaretlenmesi.

## Dependencies

- V0-ARC-003

## Blocker

- Partner API v2.0.2 kamuya açıktır; Partner Portal credential, sandbox ve imzalı veya tokenlı gerçek webhook transcript
  kanıtı çalışma alanında yoktur.
- Görev ancak yürürlükteki Partner contract, Partner Portal credential ve sandbox endpoint erişimi sağlandığında
  `Planned` durumuna alınabilir. Gerçek webhook transcript'i `Done` acceptance kanıtıdır.

## Deliverables

- V0-YSP-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Gerçek partner sandbox/portal erişimi ve imzalı ya da tokenlı webhook örneği doğrulanmış.

## Handoff

- V12-ONL-001
- V12-ONL-002
- V12-MAP-002

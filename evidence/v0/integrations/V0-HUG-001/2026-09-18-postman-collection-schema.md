# V0-HUG-001 - TokenX Documentation Postman koleksiyonu (2026-09-18)

- Task ID: V0-HUG-001
- Bu belge Status'u değiştirmez — Task hâlâ `Blocked`. Semih'in Token'ın
  resmi Postman workspace'inden (`documenter.getpostman.com/view/29891759/
  2sB34hEzUj`, koleksiyon adı "TokenX Documentation") export ettiği
  `.postman_collection.json`'un analizidir. Ham dosya bu klasörde saklı:
  `tokenx-documentation.postman_collection.json`.
- Bu, [[2026-09-18-public-docs-research]] dosyasındaki bulguları
  tamamlıyor — orada eksik kalan tam endpoint listesi ve gerçek
  request/response örnekleri burada var.

## Tam endpoint listesi (`{{TEST_*_URL}}` base URL'leri, environment ile geliyor)

| Klasör | Method | Path | Header |
|---|---|---|---|
| Auth | POST | `/v1/auth/token` | Basic Auth: `{{TEST_CLIENT_ID}}`/`{{TEST_CLIENT_SECRET}}` |
| Client Settings | POST | `/v1/client-settings` | — |
| Client Settings | GET | `/v1/client-settings` | — |
| Basket | POST | `/v1/basket` | `branch-id` |
| Basket | PUT | `/v1/basket/{{basketID}}` | `branch-id` |
| Basket | POST | `/v1/basket/unlock` | `branch-id` |
| Basket | POST | `/v1/instant-basket` | `terminal-id` |
| Basket | GET | `/v1/basket/{{BASKET_ID}}` | — (muhtemelen `terminal-id`) |
| Basket | GET | `/v1/fiscal-parameters` | `terminal-id` |
| Basket | GET | `/v1/basket/open-basket` | `terminal-id` |
| Basket | DELETE | `/v1/basket/{{BASKET_ID}}` | — |
| Organization | GET | `/v1/terminal` | `branch-id`, `merchant-id`, `terminal-id` |
| Organization | GET | `/v1/terminal/{{TEST_TERMINAL_ID}}` | `terminal-id` |

**Önemli:** Ayrı bir "Cancel" veya "Refund" endpoint'i bu koleksiyonda YOK.
`V13-HUG-003`'ün ("refund-and-cancel") varsayımı — Token'ın ayrı bir
refund API'si olacağı — bu kamuya açık koleksiyonla doğrulanamadı. İptal
muhtemelen `Delete Basket` (ödeme öncesi) veya terminalin kendi fiziksel
arayüzünden yapılıyor olabilir; **bu hâlâ açık bir blocker maddesi**.

## Auth akışı (gerçek örnek response, Token'ın kendi dokümantasyonundan — canlı secret değil)

`POST {{TEST_AUTH_URL}}/v1/auth/token`, Basic Auth (`username=client-id`,
`password=client-secret`). Örnek response:
```json
{
  "status": 201,
  "description": "User authenticated successfully.",
  "result": { "accessToken": "<JWT>" }
}
```
JWT payload'ı Keycloak tabanlı (`iss`: `.../auth/realms/tokenx-dtokc`),
`merchantId`, `clientId`, `scope: profile email` claim'leri taşıyor.

## Add Basket / Update Basket / Add Instant Basket gövde şeması

```json
{
  "basketID": "uuid",
  "checkNumber": 1,
  "title": "Masa 16",
  "filter": "Bahçe",
  "items": [
    { "name": "Pizza", "price": 15000, "sectionNo": 1, "taxPercent": 1000, "quantity": 1000 }
  ]
}
```
- `price`/`taxPercent`/`quantity` kuruş/binde cinsinden tam sayı görünüyor
  (15000 = 150,00 TL, 1000 = quantity 1 gibi — ondalık kaydırma net değil,
  gerçek cihazla doğrulanmalı).
- `Add Instant Basket` header'ı SADECE `terminal-id` (branch-id YOK) —
  V0-HUG-001'in kendi In scope notuyla örtüşüyor.

## Get Basket Details — tamamlanmış basket örneği (ödeme sonucu şeması)

```json
"sale": {
  "paymentItems": [
    { "amount": 22500, "BatchNo": 0, "currencyId": 0, "description": "Nakit",
      "operatorId": 0, "status": -1, "taxRate": -1, "TxnNo": 0, "type": 1 }
  ],
  "receiptNo": 6, "status": 0, "zNo": 3
}
```
- **`paymentItems[].type`**: `1` = nakit (cash), `3` = kredi kartı
  (başka bir örnekte "yarısı kredi kartı" body'sinde görüldü). Yemek kartı
  operatörleri için (`TokenFlex`/`Edenred`/`Multinet`/`Setcard`/`Sodexo`/
  `Metropol`) tip değeri bu koleksiyonda görünmüyor — **hâlâ eksik**.
- **`paymentItems[].operatorId`**: alan var ama örnekte `0` (nakit,
  operatör yok) — gerçek yemek kartı operatörü `operatorId` değerleri
  hâlâ dokümante değil. **Blocker'da kalan asıl madde bu.**
- **`paymentItems[].status`**: `-1` görülüyor (muhtemelen "iptal" —
  SSS'teki `BASKET_COMPLETED` webhook kod tablosuyla (`0`=başarılı,
  `-1`=iptal, `99`=fiş iptali) tutarlı).

## Hata kodları (gerçek örneklerden derlenmiş, önceki taramadan daha geniş)

| HTTP | `status` | Açıklama |
|---|---|---|
| 400 | 6 | No context header provided |
| 400 | 1018 | Order locked (unlock gerekiyor) |
| 400 | 1100 | Terminalde zaten açık basket var |
| 400 | 1104 | Terminal instant basket almaya uygun modda değil |
| 400 | 1105 | Basket durumu bu operasyona uygun değil (tamamlanmış basket) |
| 403 | 9 / 403 | Forbidden |
| 404 | 404 / 1006 | Kayıt bulunamadı |

## Sonuç ve güncellenmiş blocker durumu

Artık elimizde **gerçek, eksiksiz endpoint envanteri ve request/response
şeması** var (Auth, Basket CRUD, Instant Basket, Terminal sorgulama). Bu,
`V13-HUG-001`/`V13-HUG-002` gibi kod görevlerine başlandığında adapter
tasarımını büyük ölçüde hızlandıracak referans materyal.

Ama `V0-HUG-001`'in kendi Blocker'ı hâlâ tam olarak kapanmadı, çünkü:
1. **Yemek kartı operatörü `operatorId`/`type` eşlemesi hâlâ yok** —
   koleksiyon sadece nakit (`type:1`) ve kredi kartı (`type:3`) örneği
   içeriyor.
2. **Ayrı bir Cancel/Refund endpoint'i bu koleksiyonda yok** —
   `V13-HUG-003`'ün varsayımı doğrulanamadı, yeni bir açık soru.
3. **Gerçek `client-id`/`client-secret`/test terminali erişimi hâlâ yok**
   — yukarıdaki hepsi örnek/dokümantasyon verisi, gerçek bir isteği
   `TEST_AUTH_URL` gibi placeholder'lara karşı çalıştırmadık.

Task `Blocked` kalmaya devam ediyor, ama artık "hiçbir teknik bilgi yok"
değil, "teknik şema var, gerçek erişim/iki spesifik alan (operatorId
eşlemesi, cancel/refund akışı) yok" durumuna geçti.

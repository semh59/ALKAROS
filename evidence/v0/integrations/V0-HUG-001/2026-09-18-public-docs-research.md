# V0-HUG-001 - Kamuya açık Token/Beko dokümantasyon taraması (2026-09-18)

- Task ID: V0-HUG-001
- Bu belge Status'u değiştirmez — Task hâlâ `Blocked`. Aşağıdaki bulgular
  yalnız `developer.tokeninc.com`'un kamuya açık dokümantasyonundan
  derlenmiştir; imzalı sözleşme, gerçek client-id/client-secret, test
  cihazı erişimi veya ödeme kartı operatörü aktivasyon durumu içermez.

## Taranan kaynaklar

- <https://developer.tokeninc.com/token-developer-portal-1/x-platform/token-x-connect-cloud/gelistirici-dokumani-tr.md>
- <https://developer.tokeninc.com/token-developer-portal-1/x-platform/token-x-connect-cloud/sik-sorulan-sorular.md>
- <https://developer.tokeninc.com/token-developer-portal-1/x-platform/token-x-connect-cloud/entegrasyon-kilavuzu-and-ipuclari.md>
- <https://developer.tokeninc.com/token-developer-portal-1/baslangic.md>
- <https://developer.tokeninc.com/token-developer-portal-1/x-platform/destek/gelistirici-destek.md>
- <https://xci.devtokeninc.com/> (TokenX Connect Client App / simülatör)
- <https://documenter.getpostman.com/view/29891759/2sB34hEzUj> (Postman koleksiyonu, üçüncü taraf barındırma)

## Bulgular

### Kimlik doğrulama

- `client-id`/`client-secret`: test aşamasında Token'ın geliştirici ekibi
  tarafından paylaşılıyor; production için devops/security ekibi e-posta
  ile dağıtıyor. **Bu hâlâ self-servis değil — talep/başvuru gerektiriyor.**
- Auth akışı: `POST {TEST_AUTH_URL}/v1/auth/token`, Basic Auth header ile.
  Yanıt: `accessToken` (Bearer), `expiresIn` (86400 sn), `tokenType`.

### Endpoint şeması (kısmi doğrulama — V0-HUG-001 In scope ile örtüşüyor)

- **Add Basket** (liste modu): header `branch-id` veya `terminal-id` +
  `access_token`; body `items[]` (`name`, `price`, `sectionNo`,
  `taxPercent`, `quantity`), opsiyonel `checkNumber`/`title`/`note`/`filter`.
- **Add Instant Basket**: tek terminale direkt ödeme; header sadece
  `terminal-id` (asla `branch-id` değil); terminalin "instant mode"da
  olması gerekiyor.
- **Get Basket Details**: path `basketID`; header `terminal-id` +
  `access_token`; yanıt `items[]` ve `paymentItems[]` içeriyor (polling ile
  sonuç doğrulama — V0-HUG-001'in In scope maddesiyle örtüşüyor).
- **Webhook**: `callbackUrl` dinamik parametreleri destekliyor
  (`${terminal-id}`, `${basket-id}`, `${branch-or-terminal-id}`).
  `BASKET_COMPLETED` event'i statü kodları taşıyor: `0` başarılı, `-1` iptal,
  `99` fiş iptali (void). `BASKET_LOCKED`/`BASKET_UNLOCKED` de mevcut.

### Hata kodları (kısmi liste, kamuya açık dokümandan)

| Kod | Açıklama |
| ----- | ---------- |
| 0 | Başarılı |
| 1007 | Duplicate basketID |
| 1013 | Geçersiz veri formatı |
| 1018 | Basket kilitli (unlock gerekiyor) |
| 1100 | Terminalde açık basket var |
| 1104 | Terminal instant modda değil |

### Test/simülasyon kaynakları (yeni bulgu, bloker'ı hafifletmiyor ama ilerletebilir)

- `https://xci.devtokeninc.com/` — "TokenX Connect Client App", TokenX
  API'lerini simüle eden bir client uygulaması; "API Key" ve "Client
  Settings" alanları var (yani bu da bir credential istiyor — kayıt/erişim
  koşulları sayfada belirtilmemiş, JS-render edilen bir SPA olduğu için
  otomatik taramayla tam içerik çıkarılamadı, manuel tarayıcı ziyareti
  gerekebilir).
- Postman koleksiyonu (`documenter.getpostman.com/view/29891759/...`) —
  üçüncü taraf barındırma, içeriği bu oturumda doğrulanmadı.

### Destek/iletişim kanalı (yeni bulgu)

- Doğrudan email/telefon/form linki kamuya açık dokümanda YOK.
- Tek belirtilen kanal: **Token AI Support Chatbot** —
  `https://devassistant.tokeninc.com`. Credential/test cihazı/ticari
  abonelik talebinin muhtemelen bu kanaldan başlatılması gerekiyor, ancak
  bu doğrulanmadı (chatbot içeriği bu taramaya dahil değil).

### Doğrulanamayan maddeler (Blocker'da hâlâ açık)

- Yemek kartı operatörleri (TokenFlex, Edenred, Multinet, Setcard,
  Sodexo/Pluxee, Metropol) için `operatorId` eşlemesi kamuya açık
  dokümanda **bulunamadı**.
- Ticari abonelik/fiyatlandırma/AppStore aktivasyon süreci kamuya açık
  dokümanda **bulunamadı**.
- Sandbox/timeout/cancel/refund akışlarının API detayı kamuya açık
  dokümanda **bulunamadı**.

## Sonuç

Task Status `Blocked` kalıyor. Bir sonraki somut adım: Semih'in
`https://devassistant.tokeninc.com` üzerinden (veya bilinen bir ticari
temsilci varsa doğrudan) client-id/client-secret talebi başlatması ve
`xci.devtokeninc.com` simülatörüne erişim koşullarını netleştirmesi. Bu
adım ALKAROS kod tabanı dışında, ticari bir aksiyon — bu görev bunu
üretemez.

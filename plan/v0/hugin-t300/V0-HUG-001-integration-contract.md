# V0-HUG-001 - Validate Token/Beko integration contract

- Task ID: V0-HUG-001
- Status: Blocked
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.6
- PDF:I.6.1
- EXT:TOKEN-DEVELOPER-PORTAL
- CORR:C98

## Goal

**PDF `I.6.1`'in kilidi override edildi (2026-09-17, `V0-GOV-064`/CORR:C98,
Semih onaylı):** PDF şu anda hâlâ "Cihaz seçimi yeniden açılmayacaktır.
Hedef cihaz Hugin T300'dür." diyor (`plan/PDF_COVERAGE.md`'de değişmeden
duruyor, bu bir tarihi kayıt), ama bu artık bağlayıcı değil. Yeni hedef:
**Token/Beko (300 TR veya X30 TR)**. Task ID `V0-HUG-001` olarak kalıyor
(cross-reference kırılmasın diye) — "Hugin"/"T300" artık yalnız tarihi bir
kimlik etiketi, içeriğin kendisi Token'ı doğrular.

**Entegrasyon yolu kararı (2026-09-17, `V13-GOV-005`, Semih onaylı):**
Token üç ayrı entegrasyon yüzeyi sunuyor — kablolu (`IntegrationHub.dll`,
Windows-only, `ALKAROS.Host`'un Linux container'ıyla uyumsuz), bulut
(`TokenX Connect Cloud`, REST, container'dan sorunsuz) ve In-App
(Android, cihaz üzerinde çalışan özel uygulama). V1.3 kasa akışı için
**birincil hedef `TokenX Connect Cloud`'dur** — kablolu yol izlenmiyor,
Android/taşınabilir yol "belki sonra" diye ertelendi (bkz.
`docs/domain/token-integration-path-decision.md`).

Token/Beko (300 TR / X30 TR) payment, fiscal, timeout, unknown, cancellation,
refund ve reconciliation sözleşmesini model, firmware ve topology
düzeyinde gerçek doküman ve erişimle doğrulamak — birincil olarak
`TokenX Connect Cloud` (REST/bulut) üzerinden.

## Owned surface

- `evidence/v0/integrations/V0-HUG-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Token/Beko model/firmware desteği (300 TR / X30 TR), **birincil olarak
  `TokenX Connect Cloud` (REST/bulut)** — credentials (client-id/
  client-secret), test device, request IDs, error codes, recovery
  operations, ve polling (`Get Basket Details`) ile sonuç doğrulama.
  Kablolu (`IntegrationHub.dll`) yol yalnız tarihi karşılaştırma amacıyla
  belgelenir, izlenmez.
- Payment komutunun ALKAROS tarafından terminale gönderilmesi (`Add
  Basket`/`Instant Basket`, ödeme türü/yemek kartı operatörü seçimi) ile
  terminalin sonucu geri döndürmesi (polling ile `Get Basket Details`)
  arasındaki davranış farkı.
- Desteklenen yemek kartı operatörlerinin (TokenFlex, Edenred, Multinet,
  Setcard, Sodexo/Pluxee, Metropol) her biri için gerçek `operatorId`
  eşlemesi ve fiili aktivasyon durumu.

## Out of scope

- Production Token adapter uygulaması.
- Alternatif cihaz seçimi veya Token/Beko yerine otomatik provider/model
  fallback'i (bu görevin kendisi zaten Hugin'den Token'a bir istisnai
  geçiş — bir daha otomatik geçiş yapılmaz, yeni bir değişiklik yine
  kullanıcı onaylı ayrı bir plan değişikliği gerektirir).

## Dependencies

- V0-CMP-001
- V0-DOM-003

## Blocker

- `developer.tokeninc.com`'un kamuya açık dokümantasyonu
  `IntegrationHub.dll`'in JSON şemasını (`sendBasket`, `type`/`operatorId`)
  ve `TokenX Connect`'in REST akışını (client-id/secret → access_token)
  gösteriyor, ancak bu bir entegrasyon SÖZLEŞMESİ değil — canlıya geçiş
  için gerçek Token client-id/client-secret, terminal başına Token
  AppStore üyeliği/aktivasyonu ve ücretli ticari abonelik gerekiyor.
- Hangi yemek kartı operatörlerinin (TokenFlex/Edenred/Multinet/Setcard/
  Sodexo/Metropol) hangi ALKAROS işletmesinde fiilen aktif olacağı, her
  biri için ayrı üye işyeri sözleşmesine bağlı — Token'ın "destekli
  ödeme tipleri" listesinde olması otomatik aktivasyon anlamına gelmiyor.
- İmzalı model/firmware/protocol matrisi, entegrasyon sözleşmesi,
  erişilebilir test cihazı ve success/decline/timeout/query/cancel/
  refund/daily-total transcript kanıtı çalışma alanında yoktur.
- Görev, Token'ın kesin topology/endpoint desteğini yazılı doğrulaması ve
  test device/credential erişimi sağlandığında `Planned` durumuna
  alınabilir. Gerçek transcript'ler `Done` acceptance kanıtıdır. Olumsuz
  cevap başka cihaza otomatik geçiş yetkisi vermez; görev `Blocked` kalır
  ve ayrı, kullanıcı onaylı plan değişikliği gerekir.

## Deliverables

- Token/Beko model, firmware, protocol ve topology kombinasyonunu
  sabitleyen tarihli evidence package.
- Endpoint capability matrisi ve success, decline, timeout, query, cancel,
  refund ile daily-total gerçek çıktıları.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Gerçek Token/Beko cihazı üzerinde seçilen topology ile success, decline,
  timeout, query, cancel, refund ve daily-total/reconcile kanıtı vardır;
  ALKAROS'un payment başlatma davranışı açıkça doğrulanmıştır.

## Handoff

- V13-HUG-001
- V13-HUG-002
- V13-HUG-003
- V13-HUG-004
- V13-FSC-004

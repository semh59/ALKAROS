# V1-CUI-010 - Kasa ürün ızgarasında kalan adet rozeti

- Task ID: V1-CUI-010
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`cashier-app.js`'in çizdiği ürün kartlarında, V1-WTR-054'ün paylaşılan
`GET /api/v1/terminals/{terminalId}/catalog` uç noktasına eklediği
`remainingCount` alanını göstermek — V1-WTR-055'in Garson tarafındaki aynı
işinin Kasa karşılığı. Aynı endpoint, aynı alan; yalnız çizim tarafı Kasa'nın
kendi DOM'unda.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.js
  (V1-RMD-083 ailesinin sahipliğinde kalır) — yalnız ürün kartı render
  fonksiyonuna kalan-adet rozeti eklenir; mevcut alan/akış değişmez. Aynı
  fonksiyonda önceden var olan, bu görevden bağımsız bir eşleşmeme hatası da
  (`.product-name`/`.product-price` render ediliyor ama CSS'te yalnız
  `.pos-product-name`/`.pos-product-price` tanımlı — üçüncü sınıf,
  `.product-badge`, hiç tanımlı değil) aynı üç satırda düzeltilir: JS,
  CSS'in zaten var olan Faz 0 uyumlu isimlerine hizalanır.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.css
  (V1-CUI-008 sahipliğinde kalır) — Garson'un `.product-stock`/`.is-low`
  ile aynı görsel dili veren iki yeni kural eklenir; mevcut kurallar
  değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/Cashier/Frontend/
  altına yeni test_cashier_remaining_count.py eklenir (V1-CUI-008
  sahipliğinde kalır) — mevcut testler değiştirilmez.
- `evidence/V1-CUI-010/**`

## In scope

- `remainingCount` `null` ise hiçbir rozet gösterilmez (mevcut davranış).
- `remainingCount > 0` ise ürün kartında Garson'un `.product-stock`/`.is-low`
  ile aynı görsel dili (mümkünse aynı sınıf adları, Cashier'ın kendi CSS
  değişken adlarına bağlanarak — V1-CUI-008 token geçişinden sonra ikisi de
  aynı Faz 0 paletini kullanacağı için görsel dil zaten birleşik olacak).
- Stoku tükenen ürün zaten V1-WTR-054 sayesinde bu uç noktanın yanıtında hiç
  yer almıyor — Kasa tarafında da ayrıca "Tükendi" mantığı KURULMAZ.

## Out of scope

- Yarış durumu/çakışma koruması — V1-WTR-055'teki gerekçeyle aynı: gerçek
  koruma gönderme anında `OrderStockConsumptionService` ile zaten var,
  burada yeniden yazılmaz.
- Reçete-tabanlı hesaplama — V1-WTR-054'ün kendi kapsam dışı kararıyla aynı.

## Dependencies

- V1-WTR-054

## Acceptance evidence

- `dotnet build src/Clients/Cashier/ALKAROS.Cashier.csproj -c Debug` → 0/0.
- Yeni `test_cashier_remaining_count.py` + mevcut `test_cashier_frontend.py`
  → 5/5 geçti.
- `npx vitest run src/vanilla-clients-a11y.test.ts` (PosTerminal, Cashier+
  WaiterPwa shell'lerini axe ile denetliyor) → 6/6 geçti, yeni kontrast
  ihlali yok.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Yol boyunca bulunan ve aynı üç satırda düzeltilen önceden var olan hata:
  ürün kartı `.product-name`/`.product-price` render ediyordu ama CSS'te
  yalnız `.pos-product-name`/`.pos-product-price` tanımlıydı — üçü de bu
  görevden önce, bu görevle ilgisiz olarak zaten bozuktu; JS artık CSS'in
  var olan isimlerine hizalı.
- **Ayrı, daha ciddi bulgu — bu görevde DÜZELTİLMEDİ, kullanıcıya bildirildi:**
  `loadCatalog()`'un ürün eşlemesi `p.currentPrice ?? p.price ?? 0` ve
  `p.categoryId` okuyor; gerçek DTO alanları `unitPrice`/`categoryCode`
  (bkz. `tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs`
  satır 18). Sonuç: Kasa'da her ürün ₺0,00 gösteriyor ve kategori
  filtrelemesi "Diğer/uncategorized"e düşüyor olabilir — V1-RMD-129'un
  `waiter-app.js` için düzelttiği AYNI hata sınıfının Cashier'daki hiç
  denetlenmemiş eşi. Farklı kök neden/etki alanı olduğu için bu görevin
  kapsamına alınmadı, ayrı bir göreve bırakıldı.
- Semih'in elle deneyebileceği senaryo: V1-WTR-054'teki test ürününü (2 adet
  kalan) Kasa ekranından aç, kart üzerinde "Kalan 2" rozetini gör.

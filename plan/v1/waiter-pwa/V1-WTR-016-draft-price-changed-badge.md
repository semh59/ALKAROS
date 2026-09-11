# V1-WTR-016 - Taslakta fiyat değişti işareti

- Task ID: V1-WTR-016
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`garson-karsilastirma` karşılaştırma dokümanının "Yeni fikirler"
bölümünden dördüncü madde (Katman A, son kalanı). V1-RMD-176 kataloğu
artık arka planda (görünürlük değişince, 15 dakikadan eskiyse) yeniliyor
— ama garsonun elindeki gönderilmemiş bir taslak satırının fiyatı,
eklendiği andan kalma bir kopya. Katalog o sırada yenilenirse garson
göndermeden önce hiçbir şey görmüyordu.

**Önce gerçek riski netleştirmek gerekti:** `OrderManagementStore
.CreateTableDraftAsync` fiyatı asla istemcinin gönderdiği `unitPrice`'tan
almıyor — her zaman kataloğu kendi sorguluyor
(`ResolveCatalogProductsAsync`). Yani bu hiçbir zaman bir PARA hatası
değildi; yalnızca "garson gönderdikten sonra gerçek tutarın farklı
çıkmasına şaşırıyor" türünden bir UX sorunuydu. Düzeltme buna göre
tamamen istemci tarafında: taslak satırının önbelleğe alınmış
`line.price`'ı ile `state.products`'taki GÜNCEL fiyat farklıysa, satırın
altında küçük bir uyarı + "Güncelle" düğmesi (satırı gerçek fiyata
eşitler, göndermeden önce). Sunucu tarafında hiçbir değişiklik yok.

**"Stok değişti" yarısı bilinçli olarak kapsam dışı bırakıldı:**
`/catalog` uç noktasının yanıtı hiçbir stok bilgisi taşımıyor —
`state.products`'ın hiçbir öğesinde stok alanı yok (yalnız gönderilmiş
bir siparişin kalemleri, `AvailableStockQuantity`, sunucudan geliyor).
Bunu düzgün yapmak Catalog/Inventory sınırına yeni bir alan eklemeyi
gerektirir — bu görevin kapsamı dışında, ayrı bir görev.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-016-draft-price-changed-badge.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js, waiter-app.css
    (V1-WTR-010 sahipliğinde) — `renderDraftLine`'ın fiyat karşılaştırması
    ve uyarı işareti, `data-update-price` işleyicisi,
    `refreshCatalogIfStaleAsync`'in artık `renderBill()` de çağırması.

## Out of scope

Stok değişti işareti — `/catalog`'un stok alanı taşımaması nedeniyle ayrı
bir görev (Catalog/Inventory'ye yeni bir alan eklemeyi gerektirir).

## Dependencies

- V1-RMD-176

## Acceptance evidence

- `node --check waiter-app.js` → temiz; `waiter-app.css` parantez dengesi
  249/249.
- Sunucu tarafında değişiklik yok — `OrderManagementStore
  .CreateTableDraftAsync` kodu okunarak fiyatın gerçekten her zaman
  kataloğundan geldiği (istemcinin `unitPrice`'ının hiç okunmadığı)
  doğrulandı; bu görevin bir para riskini kapatmadığı, yalnızca bir UX
  sorununu kapattığı bu incelemeye dayanıyor.
- Bu görev için ayrı bir otomatik test eklenmedi — repoda bu dosyalar için
  JS/DOM test altyapısı yok (V1-RMD-174/176'da da aynı gerekçeyle
  kaydedildi); değişiklik kod incelemesiyle doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyada 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None

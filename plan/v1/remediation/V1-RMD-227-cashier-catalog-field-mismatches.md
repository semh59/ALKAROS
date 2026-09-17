# V1-RMD-227 - Cashier katalog alan adı uyuşmazlığı: fiyat ve kategori

- Task ID: V1-RMD-227
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`cashier-app.js`'in `loadCatalog()` fonksiyonu, paylaşılan
`GET /api/v1/terminals/{terminalId}/catalog` uç noktasından gelen ürünleri
gerçek DTO alan adlarıyla DEĞİL, hiç var olmayan adlarla okuyor:

```js
price: Number(p.currentPrice ?? p.price ?? 0)   // gerçek alan: unitPrice
categoryId: p.categoryId || 'uncategorized'      // gerçek alan: categoryCode
```

Gerçek sözleşme `CatalogProductDto` (`productId/sku/name/categoryCode/
categoryName/unitPrice/taxRate/...`) —
`tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs`
satır 18'de doğrulanmış. Sonuç: `currentPrice` ve `p.price` hiçbir zaman
var olmadığı için HER ürün `₺0,00` gösteriyor; `categoryId` hiç var
olmadığı için tüm ürünler "Diğer/uncategorized" kategorisine düşüyor,
gerçek kategori sekmelerine göre filtrelenemiyor.

Bu, V1-RMD-129'un `waiter-app.js` için düzelttiği AYNI hata sınıfının
Cashier'daki, o denetimde hiç kontrol edilmemiş eşi (V1-CUI-010 sırasında
2026-09-16'da bulundu, ayrı kök nedene sahip olduğu için o görevin
kapsamına alınmadı).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.js
  (V1-RMD-083 ailesinin sahipliğinde kalır) — yalnız `loadCatalog()`'un ürün
  eşleme satırları düzeltilir; mevcut akış/diğer fonksiyonlar değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/Cashier/Frontend/
  altına yeni bir test eklenir (V1-CUI-008 sahipliğinde kalır) — mevcut
  testler değiştirilmez.
- `evidence/V1-RMD-227/**`

## In scope

1. `price: Number(p.unitPrice ?? 0)` — gerçek alan adı.
2. `categoryId: p.categoryCode || 'uncategorized'` — gerçek alan adı.
3. Regresyon testi: gerçek DTO şeklindeki bir sahte yanıtla (`unitPrice`,
   `categoryCode` alanlarını taşıyan) `loadCatalog()`'un ürettiği
   `state.products`'ın doğru fiyat/kategori taşıdığının statik/davranışsal
   doğrulaması.

## Out of scope

- `name`/`code`/`categoryName` eşlemeleri — bunlar zaten ya doğru ya da
  ikinci fallback'leri sayesinde kazara doğru çalışıyor, bu görev onları
  değiştirmez (V1-RMD-129'un kendi kapsamındaki gibi tam bir yeniden yazım
  değil, yalnız gerçekten kırık iki alan düzeltiliyor).
- V1-CUI-010'da eklenen kalan-adet rozeti — o zaten doğru alanı
  (`remainingCount`) kullanıyor, bu hatadan etkilenmedi.

## Dependencies

- None

## Acceptance evidence

- `node --check src/Clients/Cashier/wwwroot/cashier-app.js` → geçti.
- Yeni `test_cashier_catalog_field_mapping.py` → 1/1 geçti: gerçek DTO alan
  adlarının (`unitPrice`, `categoryCode`) kullanıldığı, eski kırık adların
  (`currentPrice`, `p.categoryId ||`) artık kaynak kodda olmadığı doğrulandı.
- `tests/Clients/Cashier/Frontend/` (tamamı) → 6/6 geçti, regresyon yok.
- `dotnet build src/Clients/Cashier/ALKAROS.Cashier.csproj -c Debug` → 0 Uyarı, 0 Hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Değişiklik yalnız izin verilen dosyalar (git diff ile doğrulandı):
  `cashier-app.js` (yalnız iki eşleme satırı) + yeni test dosyası.
- Semih'in elle deneyebileceği senaryo: Kasa ekranını aç, gerçek fiyatlı bir
  ürünün artık ₺0,00 değil gerçek fiyatını gösterdiğini ve kategori
  sekmelerine göre doğru filtrelendiğini doğrula.

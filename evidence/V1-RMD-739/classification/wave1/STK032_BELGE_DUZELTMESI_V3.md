# STK-032 — stok bakiye projeksiyonu migration yolu düzeltmesi

Bu not V1-RMD-739 programının güncel belge eşlemesidir; eski kapanış kanıtını yeniden yazmaz, yeni test sonucu üretmez.

- Özgün kaynak: evidence/V11-INV-002/closure-evidence.txt:19-23 ve :62-63.
- Tarihsel metin: database/migrations/V11/V11-INV-002/058-stock-balances.up.sql ve .down.sql.
- Bugünkü gerçek yollar:
  - database/migrations/V11/V11-INV-002/061-stock-balances.up.sql
  - database/migrations/V11/V11-INV-002/061-stock-balances.down.sql
- Up dosyası Migration 061 ve V11-INV-002 kimliğini taşır; inventory.stock_balances ve uq_stock_balances_item_location kısıtını tanımlar.
- 058 referansı bugünkü migration yürütme yolu değildir; güncel komutlar 061 dosyalarını kullanmalıdır. Bu not geçmiş dosya içeriklerinin eşit olduğunu iddia etmez.
- Eski 12 test ve build sonucu tarihsel iddiadır; burada test/build/migration ileri-geri koşulmadı.
- Bu yalnız yol düzeltmesidir. Rebuild kusurları ayrı kalır: STK-006/007/008 açık; STK-009 karar engelli. Bir belge notu bunları kapatmaz.
- Provenance: git rename commit df26505dc3c912eda01436112f362d31b8105f3f, 2026-09-06.
- Özgün ham günlük korunur; güncel keşif noktası BELGE_DUZELTMELERI_V3.md. Eski belgedeki metin değiştirilmediğinden tam belge remediasyonu iddia edilmez.

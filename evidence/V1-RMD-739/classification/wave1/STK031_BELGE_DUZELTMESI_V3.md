# STK-031 — stok hareket defteri migration yolu düzeltmesi

Bu not V1-RMD-739 doğrulama programının güncel belge eşlemesidir; eski kapanış kanıtını yeniden yazmaz, yeni test sonucu üretmez.

- Özgün kaynak: evidence/V11-INV-001/closure-evidence.txt:21-25 ve :61-62.
- Tarihsel metin: database/migrations/V11/V11-INV-001/057-stock-movements.up.sql ve .down.sql.
- Bugünkü gerçek yollar:
  - database/migrations/V11/V11-INV-001/060-stock-movements.up.sql
  - database/migrations/V11/V11-INV-001/060-stock-movements.down.sql
- Up dosyasının ilk satırı Migration 060 ve V11-INV-001 kimliğini taşır. İçerik inventory.stock_movements ve immutable tetikleyicisini tanımlar.
- 057 referansı eski migration numaralandırmasına aittir; bugünkü komutlarda 060 dosyaları kullanılmalıdır. Bu not içeriklerin tarih boyunca birebir aynı kaldığını iddia etmez.
- Eski belgedeki 18 test ve build sonucu tarihsel iddiadır; burada test/build/migration ileri-geri koşulmadı.
- Provenance: git rename commit df26505dc3c912eda01436112f362d31b8105f3f, 2026-09-06; benzerlik up %96, down %77.
- Özgün ham günlük korunur. Güncel düzeltmenin keşif noktası: BELGE_DUZELTMELERI_V3.md.
- Sınır: Eski plan/günlüğün kendi içindeki bayat metni değiştirilmedi; yalnız güncel doğrulama raporunda doğru yollar karşılandı. Özgün bulgunun tüm belgelerde kapanışı ayrıca kontrol edilmelidir.

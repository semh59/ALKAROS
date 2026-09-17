# V1-WTR-056 - Kalan stok rozetini paylaşılan bir yardımcıya çıkar

- Task ID: V1-WTR-056
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-16

## Goal

`bill.js`'in `renderSentLine`'ı (V1-RMD-143, `item.availableStockQuantity`)
ve `menu.js`'in `renderProducts`'ı (V1-WTR-055, `product.remainingCount`)
aynı `.product-stock`/`.is-low`/`.is-out` rozetini iki ayrı yerde elle
yeniden yazıyor. Eşik değeri (5) veya CSS sözleşmesi ileride değişirse biri
güncellenip diğerinin unutulması riski var — 2026-09-16'daki bağımsız
`/code-review high` incelemesinin bulduğu tek gerçek sorun buydu. Tek bir
`renderStockBadge(count)` yardımcısına çıkarılır, ikisi de onu çağırır.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/util.js
  (V1-WTR-037 sahipliğinde kalır) — yeni `renderStockBadge(count)` saf
  fonksiyonu eklenir, mevcut export'lar değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/sheets/bill.js
  (V1-WTR-045 sahipliğinde kalır) — `renderSentLine`'daki elle yazılmış
  rozet bloğu `renderStockBadge` çağrısına indirgenir, `is-out` durumu
  (bu dosyada geçerli, sepete zaten eklenmiş kalem için) korunur.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/screens/menu.js
  (V1-WTR-055 sahipliğinde kalır) — `renderProducts`'taki elle yazılmış
  rozet bloğu `renderStockBadge` çağrısına indirgenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/WaiterPwa/Frontend/test_menu_remaining_count.py
  (V1-WTR-055 sahipliğinde kalır) — yardımcı fonksiyona taşınan davranışı
  hâlâ doğrulayacak şekilde güncellenir.
- `evidence/V1-WTR-056/**`

## In scope

- `renderStockBadge(count, { allowOut })` (veya eşdeğeri): `count` null/
  undefined ise `''`; `allowOut` true ve `count <= 0` ise `.is-out`
  ("Tükendi"); `count < 5` ise `.is-low`; aksi hâlde düz `.product-stock`.
  Eşik (5) ve metin ("Kalan N") TEK yerde yaşar.
- `bill.js` ve `menu.js` bu fonksiyonu `util.js`'ten import eder, kendi
  rozet HTML'ini elle kurmaz.
- Görsel çıktı (CSS sınıfları, metin) davranışsal olarak AYNI kalır —
  bu saf bir kod tekrarı temizliği, yeni bir görünüm değil.

## Out of scope

- `.product-stock` CSS kurallarının kendisi (zaten var, değişmiyor).
- Eşik değerinin (5) değiştirilmesi — yalnız tek yere taşınıyor.

## Dependencies

- V1-WTR-054
- V1-WTR-055

## Acceptance evidence

- `node --check` ile üç dosyanın da (util.js, bill.js, menu.js) sözdizimi
  doğrulandı, hepsi geçti.
- `pytest tests/Clients/WaiterPwa/Frontend/` → 6/9 geçti; 3 başarısız test
  AYNI önceden-var-olan 3 test (JS modülerleştirme refactor'ünden kalma,
  bu görevden önce de bozuktu, ilgisiz) — yeni regresyon yok.
- Yeni/güncellenen `test_menu_remaining_count.py` → 3/3 geçti: rozetin artık
  `util.js`'te TEK yerde yaşadığı, `bill.js`'in de aynı yardımcıyı çağırdığı
  doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `escapeHtml`/`formatQuantity` import'larının her iki dosyada da (bill.js,
  menu.js) başka kullanım noktaları olduğu doğrulandı — ölü import kalmadı.
- Semih'in elle deneyebileceği senaryo: Garson ekranında hem menü kartında
  hem gönderilmiş bir kalemde aynı ürün için "Kalan N" rozetinin birebir
  aynı metin/renk kuralıyla göründüğünü doğrula.

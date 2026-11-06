# V1-RMD-853 - Kasa istemcisi: dize arayan testler davranış testine çevrilir

- Task ID: V1-RMD-853
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9 (F0355, F0360, F0386, F0059, F0060, F0065, F0078, F0079, F0668): Kasa ve Garson istemcilerinin davranışı yalnız kaynak metninde dize aranarak sınanıyor (`tests/Clients/Cashier/Frontend/*.py`, `test_waiter_catalog_retry.py`); `test_cashier_frontend.py:95` kodda hiç geçmeyen `orders/table/` dizesini aradığı için kırık; `cashier-app.test.js` kataloğu gerçek olmayan `currentPrice`/`categoryId` alanlarıyla besliyor (fiyat ve kategori hiç sınanmıyor). Bu dize testleri jsdom davranış testlerine çevrilir (park/geri yükleme onayı, kalan-adet rozeti, gerçek katalog alanlarıyla fiyat/kategori, katalog hatasında kapalı kalma, arama kuralları ve boş sonuç iletisi, manifest ve işaretleme sözleşmesi, garson menüsünde yeniden deneme) ve yerlerini aldıktan sonra silinir.

## Owned surface

- `evidence/V1-RMD-853/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/cashier-app.test.js — katalog fixture'ının gerçek alanlara düzeltilmesi.
  - tests/Clients/StaticApps/cashier-ticket-and-catalog.test.js — yeni davranış testleri.
  - tests/Clients/StaticApps/waiter-app.test.js — menü yeniden deneme davranışı.
  - tests/Clients/Cashier/Frontend/test_cashier_frontend.py — silinir (davranış testlerinin karşılığı).
  - tests/Clients/Cashier/Frontend/test_cashier_remaining_count.py — silinir.
  - tests/Clients/Cashier/Frontend/test_cashier_catalog_field_mapping.py — silinir.
  - tests/Clients/Cashier/Frontend/test_cashier_catalog_search.py — silinir.
  - tests/Clients/WaiterPwa/Frontend/test_waiter_catalog_retry.py — silinir.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı: yeni davranış testleri mevcut üretim kodunda yazılır; gerçek alan adlarıyla katalog fiyatı/kategorisi, park/geri yükleme onayı vb. Üretim kodunda kusur çıkarsa kırmızı olur ve yüzey dışı olduğundan ayrı göreve devredilir.
2. Her silinen Python testinin koruduğu her iddia bir davranış testine eşlenir (RESULT'ta tablo).
3. Mutasyon: geri yükleme onayı, kalan-adet eşiği (5), alan eşlemesi, fail-closed ve Türkçe küçük harf arama kuralları bozulunca testler düşer (cp+cmp).

## Out of scope

- Kasa ve Garson üretim kodu; E2E (Playwright) senaryoları.

## Dependencies

- None

## Acceptance evidence

1. StaticApps tam koşusu ve kalan Python testleri exit 0; çıktı `evidence/V1-RMD-853/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

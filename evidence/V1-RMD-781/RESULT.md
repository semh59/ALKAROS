# V1-RMD-781 sonuç

Baseline: 594fa6d1. Değişen: `deploy/docker/seed-demo.sql`, `compose.dev.yaml` (yalnız açıklama), TableDraft test projesi (csproj bağlantısı, test veritabanı yardımcısı, 3 test).

## Stok kuralı (koddan)
- Eşlemesiz ürün: `OrderStockConsumptionService.ConsumeItemsAsync` -> `ProductStockNotConfiguredException`. Kural değişmedi.
- Tüketim: `product_stock_mappings` -> `stock_items.default_location_id` -> `TryApplyGuardedOnHandDeltaAsync(-adet*çarpan)`; ek olarak `Consumption` defter hareketi.
- Bakiye `stock_balances` bir izdüşümdür; `StockBalanceProjector` / `ReplaceAllBalancesAsync` bakiyeyi defter toplamlarıyla değiştirir (`In` +, `Out` -). Bu yüzden tohum, `Adjustment`/`In`/`Manual` defter hareketi + aynı miktarda bakiye satırı yazar.
- Negatif bakiye: `trg_stock_balances_non_negative` (087). Tohum 500 adet açar.
- Sabit kimlikler: konum `f0000000-...-01`, stok kalemleri `f1000000-...-0N`, hareketler `f2000000-...-0N`, bakiyeler `f3000000-...-0N`. Hepsi `ON CONFLICT DO NOTHING`; yeniden çalıştırma satılmış stoku sıfırlamaz.

## Kanıt
- Kırmızı: `red/` — tohumsuz hâlde 3 yeni test kırık (eşleme 0/6; bakiye okunamıyor).
- Yeşil: `green3/` (inceleme sonrası güçlendirilmiş testler: 6 ürünün tamamı satılır, çarpan=1 ve tek eşleme denetlenir) — TableDraft projesi 115/115 gerçek Postgres'te (`green-attempt1-disposed-datasource/` test yardımcısındaki kaynak yönetimi hatasıyla kırık ilk deneme).
- Mutasyon (cp+cmp ile geri alındı): eşleme bloğu çıkarılınca 3/3 kırık (`mut-no-mapping/`); defter hareketi çıkarılınca defter=bakiye testi kırık (`mut-no-ledger/`); bakiye çıkarılınca 3/3 kırık (`mut-no-balance/`); bakiyede `DO UPDATE` ile sıfırlama kırık (`mut-balance-do-update/`); bir eşlemede çarpan 2 kırık 3/3 (`mut-multiplier-2/`). (İlk üç mutasyon güçlendirmeden önceki testlerle koşuldu.)
- İncelemeler: qa-veritabani bulgu: Düşük 2 (kod çakışmasında hata mesajı açık değil ama tohum bütünüyle geri alınır; tohumdan sonra elle yeniden eşleme yapılırsa tohum ikinci eşleme ekler) kabul edildi. qa-test-kalitesi: 5 bulgunun 4'ü (tek ürün, çarpan, DO UPDATE mutantı, bakiye=defter satış sonrası) giderildi; kurulum tekrarı yardımcıda toplandı.
- Canlı yığın: `live-seed-1.log` (6+6+6+6+1 satır eklendi), `live-seed-2.log` (ikinci çalıştırma 0 satır), `live-e2e.log` + `live-db-after.log`: taslak 200, onay 200 (`Submitted`), send-to-cashier 200, hesap açıldı, nakit tahsilat 200; hesap `Paid`, sipariş `Completed`, Adana Kebap stoku 500 -> 498 = defter toplamı.
- plan-audit / consistency-audit / task-scope: `plan-audit.log` (0/0), `consistency-audit.log` (clean), `task-scope.log` (valid).

## Açık noktalar
- Docker imaj derlemesi bugünkü HEAD'de kırık: `pnpm build` içindeki oxlint 2 hata veriyor (`src/features/management-security-system/TerminalsPanel.tsx:36` boş `<th />` etiketsiz; `src/routes/CustomerDisplay.tsx:244` render içinde `Date.now()`), `docker-build.log`. Yerelde de aynı (`oxlint -c .oxlintrc.json --max-warnings=20 src`). Bu görevin yüzeyi dışında; imajlar yenilenmedi, canlı doğrulama 2026-10-17 imajlarıyla yapıldı (tohum yalnız veritabanında, imaj gerektirmez).
- `tests/Deployment/test_container_contract.py` 4 test bu görevden bağımsız zaten kırık (değişiklik geri alınınca da 4 kırık).
- Tarayıcı arayüzü (Playwright) doğrulanmadı; akış, arayüzün gönderdiği aynı yüklerle API düzeyinde doğrulandı.

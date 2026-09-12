# V1-WTR-027 - Eşzamanlı stok düşümü deadlock'una tekrar deneme

- Task ID: V1-WTR-027
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

V1-WTR-026'nın Playwright yük testinde (6 garson aynı anda sipariş
gönderiyor) bulunan gerçek bir Postgres hatasının düzeltilmesi. Semih'in
talimatıyla ("Evet düzelt", 2026-09-12).

**Bulgu:** Birden fazla masa AYNI popüler ürünü aynı anda sipariş edince,
`PostgresStockBalanceRepository`'nin stok düşme sorgusu
(`INSERT ... ON CONFLICT (stock_item_id, stock_location_id) DO UPDATE`)
yoğun eşzamanlı talepte gerçek bir Postgres deadlock'una (`40P01`)
çarpabiliyor — bu satırın kilit sırasıyla ilgili bir hata değil (her çağrı
kendi siparişinin stok kalemlerini hep aynı sırada dokunuyor), Postgres'in
kendi `INSERT ... ON CONFLICT DO UPDATE`'inin yoğun rekabet altındaki
belgelenmiş bir köşe durumu. Sonuç: `submit-draft` 503 dönüyor ve **sipariş
gerçekten mutfağa ulaşmıyor** — bugün düzeltilen `headers` hatasıyla aynı
sınıftan bir kayıp, farklı bir sebepten.

4 ayrı yük testi koşumunda 3'ünde en az 1/6 sipariş bu şekilde başarısız
oldu.

**Çözüm:** `SubmitOrderHandler.HandleAsync` artık `PostgresErrorCodes
.DeadlockDetected` yakalayıp (en fazla 5 deneme, denemeler arası küçük
rastgele bir bekleme ile) TÜM işlemi sıfır bağlantıdan yeniden deniyor.
Kaybeden tarafın işlemi bütünüyle geri alındığı (hiçbir şey kalıcı
olmadığı) için tam yeniden deneme güvenli — kısmi bir tekrar değil.
Rastgele bekleme, aynı döngüde kaybeden birden fazla tarafın aynı anda
yeniden başlayıp aynı döngüyü tekrar oluşturma ihtimalini azaltıyor.

**Doğrulama körü körüne yapılmadı:** düzeltmeden önce yük testi arka arkaya
birkaç kez çalıştırılıp gerçek başarısızlık gözlemlendi (gerçek Host
loglarında `Npgsql.PostgresException: 40P01: deadlock detected` — tam yığın
izi bu görevin kendi commit'inde), düzeltmeden sonra AYNI test 6 kez üst
üste çalıştırılıp her seferinde 6/6 başarı elde edildi.

## Owned surface

- Sınırlı ek:
  - src/Modules/Orders/SubmitOrder/SubmitOrderHandler.cs (Orders
    sahipliğinde) — HandleAsync artık bir retry sarmalayıcısı,
    asıl mantık private HandleAttemptAsync'e taşındı.
  - tests/E2E/WaiterPwa/global-setup.js, lib/seed.js, README.md,
    specs/05-load-and-timing.spec.js (yeni) (WaiterPwa E2E paketi
    sahipliğinde, V1-WTR-026) — yük testi + 4 kişilik masa gerçek
    senaryo zamanlaması; ek yük-testi masaları (LOAD-1..6),
    E2E-TIMING masası (4 koltuk), stok miktarı 50'den 100000'e
    çıkarıldı (yoksa eşzamanlı yük testi gerçek stok yetersizliğiyle
    kirlenirdi); `E2E_HOST_LOG` hata ayıklama anahtarı.

## Out of scope

- Deadlock'un kök nedeni olan Postgres'in kendi `INSERT ... ON CONFLICT DO
  UPDATE` kilitleme davranışını değiştirmek (ör. açık `SELECT ... FOR
  UPDATE` + ayrı `INSERT`/`UPDATE`'e geçmek) — retry, mevcut sorguyu
  değiştirmeden aynı sınıftaki her geçici hatayı (yalnız deadlock değil)
  kapsayan daha küçük, daha güvenli bir düzeltme. Sorgunun kendisini
  yeniden tasarlamak ayrı bir görev olur.
- Bu retry yalnızca `SubmitOrderHandler.HandleAsync` (submit-draft/quick-sale
  gönderim) yolunu kapsıyor — `OrderManagementStore.FireCourseAsync` gibi
  stok tüketmeyen diğer yollar zaten etkilenmiyor, ayrıca dokunulmadı.

## Dependencies

- V1-WTR-026

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet
  test`:
  - `tests/Modules/Orders/SubmitOrder` → 16/16 (regresyon).
  - `tests/Host/Experience/Orders/TableDraft` → 66/66 (regresyon).
- `tests/E2E/WaiterPwa/specs/05-load-and-timing.spec.js` (gerçek Chrome +
  gerçek Postgres + gerçek Host, `node node_modules/@playwright/test/cli.js
  test specs/05-load-and-timing.spec.js`):
  - Düzeltmeden ÖNCE: 4 koşudan 3'ü başarısız (6 eşzamanlı siparişten 1-2'si
    503 ile kayboldu; gerçek `40P01` deadlock, Host loglarında doğrulandı).
  - Düzeltmeden SONRA: 6 koşu üst üste, her seferinde 6/6 başarı.
  - Gerçek 4 kişilik masa senaryosu (E2E-TIMING, koltuk seçimiyle tam UI
    akışı): masayı açmaktan mutfağa onaylanmış gönderime kadar ~2,5 saniye
    (otomatik tıklama, insan karar süresi hariç — bir taban, bir SLA iddiası
    değil).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.

## Handoff

- None

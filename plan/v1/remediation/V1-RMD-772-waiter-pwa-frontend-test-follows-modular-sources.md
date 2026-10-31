# V1-RMD-772 - Garson PWA frontend testi modüler kaynak yapısını izlesin

- Task ID: V1-RMD-772
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu (V1-RMD-051, Yüksek). `tests/Clients/WaiterPwa/Frontend/test_waiter_pwa_frontend.py` HEAD'de 9 testten 3'ü kırmızı. Kök neden kod bozukluğu değil, testin bayatlığı: Garson PWA kodu `js/` altındaki modüllere bölündü ve test hâlâ yalnız `waiter-app.js` içinde arıyor. `index.html` bölge çubuğunu `id="zoneChips"` ile sunuyor (`zoneList` yok). Cihaz terminal kimliği `js/util.js` içinde. Kategoriler ayrı uçtan değil `/catalog` yanıtından türetiliyor ve bunun nedeni `waiter-app.js` yorumunda kayıtlı: garson oturumunun çağırabileceği ayrı bir kategori ucu yok.

Çözüm: test, güncel gerçek yapıyı doğrular. Kaynak olarak `waiter-app.js` ile `js/**` birlikte okunur; `zoneChips` aranır; terminal kimliği anahtarı `js/util.js` içinde aranır; `/catalog-management/categories` ucunun çağrılmadığı ve `/catalog` ucunun kullanıldığı doğrulanır. Hiçbir doğrulama gevşetilmez, üretim kodu değişmez.

## Owned surface

- `evidence/V1-RMD-772/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/WaiterPwa/Frontend/test_waiter_pwa_frontend.py (V1-RMD-051) — güncel modüler yapıyı doğrulayan test.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Üç kırmızı testin güncel gerçek davranışa göre düzeltilmesi.
2. Düzeltilen testlerin, ilgili kaynak bozulduğunda kırmızıya döndüğünün mutasyonla gösterilmesi.

## Out of scope

- `src/Clients/WaiterPwa/**` üretim kodu, diğer testler.

## Dependencies

- V1-RMD-051

## Acceptance evidence

1. Düzeltmeden önce 3 test kırmızı (log), sonra dosyadaki 9 test yeşil.
2. Mutasyon: `index.html`'den `zoneChips`, `js/util.js`'ten terminal kimliği anahtarı ve `waiter-app.js`'e kategori ucu eklenmesi ilgili testleri kırar; kaynaklar `cmp` ile geri alınır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.

## Handoff

- None

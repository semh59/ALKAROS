# V1-RMD-781 - Docker demo tohum verisine stok kalemi, eşleme ve başlangıç bakiyesi eklensin

- Task ID: V1-RMD-781
- Status: Done
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

Canlı Docker yığınında yeniden üretildi: `deploy/docker/seed-demo.sql` 6 ürün (FOOD-001..003, DRNK-001..003, `stock_mode=1`) oluşturuyor ama hiçbir stok kalemi, ürün-stok eşlemesi, stok konumu veya bakiye yok. Kasada "onayla ve mutfağa ilet" adımında (`submit-draft`) `OrderStockConsumptionService` eşlemesiz ürün için `ProductStockNotConfiguredException` atıyor ve istek 409 `PRODUCT_STOCK_NOT_CONFIGURED` ile dönüyor. Sonuç: demo yığınında hiçbir sipariş onaylanamıyor, hesap alınamıyor.

Semih kararı: gerçek stok kuralı aynen kalır (eşlemesiz ürün siparişi reddedilir); eksik olan demo tohum verisidir. Çözüm yalnız tohum dosyasını tamamlar: her demo ürün için `Portion` türünde bir stok kalemi, ortak bir varsayılan stok konumu, çarpanı 1 olan ürün-stok eşlemesi ve yeterli başlangıç bakiyesi. Bakiye, gerçek girişle aynı yoldan yazılır: değişmez defterde (`inventory.stock_movements`) `Adjustment`/`In` hareketi ve aynı miktarda `inventory.stock_balances` satırı. Böylece defterden bakiye yeniden kurulduğunda (`ReplaceAllBalancesAsync`) bakiye silinmez. Tohum idempotenttir (sabit kimlikler, `ON CONFLICT DO NOTHING`); yeniden çalıştırmak harcanmış stoku sıfırlamaz.

## Owned surface

- `evidence/V1-RMD-781/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - deploy/docker/seed-demo.sql (V1-RMD-098) — stok kalemi, konum, eşleme, defter hareketi ve bakiye.
  - compose.dev.yaml (V1-RMD-098) — seed servisinin açıklama satırı.
  - tests/Host/Experience/Orders/TableDraft/ (V1-RMD-106) — tohumu yükleyip onay akışını doğrulayan test.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Tohuma 1 konum, 6 stok kalemi (`Portion`, birim `adet`, varsayılan konum), 6 eşleme (çarpan 1), 6 `Adjustment`/`In` hareketi ve 6 bakiye satırı; sabit kimlikler, `ON CONFLICT DO NOTHING`.
2. `compose.dev.yaml` seed servisi açıklaması stok verisini de anar.
3. Gerçek Postgres testleri: tohum yüklenince her ürünün tam bir eşlemesi, varsayılan konumu ve pozitif bakiyesi vardır; her bakiye defter toplamına eşittir; tohum ikinci kez yüklenince satır sayıları ve bakiye değişmez; tohumlu ürünle taslak onayı 200 döner ve bakiye düşer.
4. Canlı yığın: imajlar bugünkü HEAD ile yeniden derlenir, tohum mevcut veritabanına psql ile yeniden uygulanır, kasada sipariş onayı ve hesap alma uçtan uca denenir.

## Out of scope

- Stok kuralı, `OrderStockConsumptionService`, şema/migration, üretim kurulumunun stok tanımı (yönetici ekranından yapılır).
- Önceden oluşmuş test siparişlerinin silinmesi veya temizliği.

## Dependencies

- V1-RMD-098

## Acceptance evidence

1. Kırmızı önce: tohumsuz veritabanında ürün için taslak onayı 409 `PRODUCT_STOCK_NOT_CONFIGURED`; tohumla yeşil. TableDraft testleri gerçek Postgres'te yeşil.
2. Mutasyon: tohumdan eşlemeyi, bakiyeyi veya defter hareketini çıkarmak yeni testleri kırar; kaynak `cmp` ile geri alınır.
3. Canlı yığın: kasada demo ürünle sipariş onayı ve nakit ödemeyle hesap kapanışı gerçek yanıt kodlarıyla `evidence/V1-RMD-781/` altında.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. qa-veritabani ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir.

## Handoff

- None

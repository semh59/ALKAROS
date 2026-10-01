# V1-RMD-486 - Ekstraların kuramsal tüketime yazılması

Stok kalemine bağlı bir ekstra (ekstra peynir) gerçek stoktan düşüyordu ama kuramsal tüketim kaydına yazılmıyordu; gerçek ile kuramsal tüketimi karşılaştıran rapor o stok kalemi için
yapay bir fark gösteriyordu. Artık sipariş kabulünde, gerçek düşümle aynı işlemde ve aynı kalem kimliğiyle, `adet × çarpan` kadar kuramsal kayıt yazılır; ürünün reçetesi olması gerekmez.
Kalem iptal edilirse rapor mevcut kalem kimliği kuralıyla gerçek düşümü ve ekstra kaydını birlikte dışlar. Rapor sorgusu değişmedi.

## Değişiklikler

- Migration `174`: kuramsal kayıtta reçete ve sürüm boş olabilir, `modifier_id` eklendi, her kayıt ya reçeteden ya ekstradan gelir (CHECK). Geri alma ekstra kaynaklı satırları siler (tablo ekleme-yalnız olduğundan tetikleyici yalnız bu tek ifade için kapatılır).
- `TheoreticalConsumptionRecord.ForModifier`, depo `modifier_id` yazar; `OrderStockConsumptionService` ekstra kaydını reçete yolundan bağımsız yazar (reçetesiz ürün erken dönüşüne takılmaz).
- Bağlantılar: `order.json`, `MigrationManifest`, `ManifestTests`.

## Kanıt

- `tests-module.log`: kuramsal kayıt modül testleri 16/16 (ekstra kaynaklı kayıt toplamlarda sayılır, ikisi birden veya hiçbiri CHECK ile reddedilir, 174 geri alma reçete kaydını korur ekstra kaydını siler, yeniden uygulama tekrarlanabilir, alan testleri).
- `tests-host-tabledraft.log`: sipariş gönderiminde Host testi 88/88 (yeni: eşlenmiş ekstra reçetesiz üründe de kuramsal kayıt yazar, stok 10→8, kuramsal 2).
- `tests-host.log`: tam Host test projesi 174/174 (manifest testi 174 dahil).
- `mutation.log`: 3 mutant (ekstra kaydı yazılmıyor, `modifier_id` yazılmıyor, CHECK zayıflatıldı) kırmızı; dosyalar geri alındı, `fc /b` özdeş.
- `gercek-deneme.log`: gerçek Host + Postgres, NFC siparişi: 2 burger, her birine ekstra peynir (çarpan 1,5): gerçek stok hareketi peynir 3, kuramsal kayıt peynir 3 (`from_extra`); toplam 420.

## Açık kalan

- Satılan kalem başına maliyet veya kâr raporu yok; ekstranın parasal maliyeti bu görevin konusu değil.
- Doğrudan rapor uç noktası bu görevde çağrılmadı; rapor sorgusu değişmedi, kayıtlar mevcut toplama giriyor.

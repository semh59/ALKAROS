# V12-RMD-005 - Online stok ve katalog yayınının doğruluk bulgularını kapat

- Task ID: V12-RMD-005
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

2026-09-26 bağımsız Faz 3 denetiminin yayın bulgularını kapatmak (V12-ONL-004, V12-ONL-005; Semih: "en küçük hata
bile kritik"):

**Stok yayını (V12-ONL-005)**

- "Yalnız daha yeni gözlem" koruması, sürümü stok satırlarının `row_version` toplamından alıyor. Eşleme çarpanı
  değişince sürüm değişmiyor; eşlenmiş stok kalemi çıkarılınca sürüm düşüyor. İki durumda da kanalın gördüğü adet
  donuyor ve fazla satışa yol açabiliyor.
- Başarısız bir toplu gönderim en eski satırları kuyruğun başında tutuyor. Tek bir bozuk SKU arkasındaki bütün
  ürünlerin güncellemesini sonsuza kadar engelliyor.
- Başka ürüne taşınan ya da ileri tarihli bir eşlemenin eski durum satırı hiç temizlenmiyor.

**Katalog yayını (V12-ONL-004)**

- Yayınlar sırasız teslim edilebiliyor; sağlayıcıda eski fiyat kalabiliyor.
- Menü, yayın kilidinden önce okunuyor.
- Ölü kuyruğa düşen bir yayın, aynı içeriğin yeniden yayınını "değişmedi" diye engelliyor.
- Fiyatı olmayan pasif ürün `0,00` fiyatla gönderiliyor.
- Zaman aşımları kaydedilmiyor.
- SKU sahipliği kontrolü eşleme kilidinin dışında yapılıyor; eşzamanlı bir eşleme başka ürünün SKU'sunu alabiliyor.

## Owned surface

- `plan/v1.2/online-ordering/V12-RMD-005-online-publishing-correctness.md`
- `evidence/V12-RMD-005/**`
- `database/migrations/V12/V12-RMD-005/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/AvailabilityPublishing/ (V12-ONL-005) — sıralı gözlem sürümü, satır başına bekleme
    ve yalıtım, eski satırların temizliği.
  - src/Modules/OnlineOrdering/CatalogPublishing/ (V12-ONL-004) — sıra, kilit altında okuma, yeniden yayın,
    fiyatsız pasif ürün, zaman aşımı.
  - src/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/ (V12-MAP-001) — kilit altında "sahipsizse eşle",
    ileri tarihli eşlemelerin yayından çıkarılması.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs — 152 numaralı migration konumu.
  - tests/Modules/OnlineOrdering/AvailabilityPublishing/ tests/Modules/OnlineOrdering/CatalogPublishing/
    tests/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/ ve tests/Host/Experience/OnlineOrdering/ — testler ve
    fikstürler.

## In scope

1. Stok yayını:
   - Gözlem sürümü, kanal başına transaction kilidi altında alınan tek yönlü bir sıradan
     (`online_ordering.availability_observation_seq`, migration 152) gelir. Kaynak aynı kilit altında okunur.
     Böylece her yenileme bir öncekinden daha yenidir ve çarpan ya da eşleme değişikliği de uygulanır.
   - Sıra, mevcut en büyük `desired_version`'ın üstünden başlar.
   - Başarısız teslim edilen satırlar bekler (`next_attempt_at`, üstel, en çok 30 dk). Bir tur önce hiç
     başarısız olmamış satırları gönderir; yoksa beklemesi dolmuş tek bir başarısız satırı gönderir. Böylece bozuk
     SKU diğerlerini tıkamaz ve kendini ele verir.
   - Kanalın artık yayınlamadığı ürünlerin durum satırları silinir.
2. Katalog yayını:
   - Menü kilit alındıktan sonra okunur.
   - Teslim, kanal ve menü kilidi altında daha yeni bir yayın var mı diye bakar. Varsa bu yayın
     `Superseded` olur ve gönderilmez.
   - "Değişmedi" yalnız son yayın aynı içerikle `Delivered` ise verilir.
   - Fiyatı olmayan pasif ürün, kanala en son teslim edilen fiyatıyla kapatılır. Hiç yayınlanmamışsa gönderilmez.
   - Kapanıştan kaynaklanmayan zaman aşımı bir başarısız deneme olarak kaydedilir.
3. `IYemeksepetiProductMappingService.MapIfUnownedAsync`: SKU'nun başka ürüne ait olup olmadığı eşleme kilidi
   altında kontrol edilir. Yayındaki atama bunu kullanır. Açık eşleme listesi yalnız yürürlükteki eşlemeleri verir.

## Out of scope

- Outbox deneme politikası, 401'de token temizleme, durum senkronu ve sipariş yaşam döngüsü (V12-RMD-008).

## Dependencies

- V12-RMD-007

## Deliverables

- Kod, migration 152 (up/down), testler.

## Acceptance evidence

- İlgili test projeleri yeşil; mutasyon kontrolü `evidence/V12-RMD-005/` altında.
- `task_scope_tool.py --task-id V12-RMD-005 --diff-base <InProgress commit>` exit 0.

## Handoff

- None

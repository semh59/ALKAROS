# V12-RMD-008 - Online durum senkronu ve sipariş yaşam döngüsü bulgularını kapat

- Task ID: V12-RMD-008
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

2026-09-26 bağımsız Faz 3 denetiminin durum senkronu ve yaşam döngüsü bulgularını kapatmak (V12-ONL-002,
V12-ONL-003, V12-REC-001; Semih: "en küçük hata bile kritik"):

- Sağlayıcıya giden durum ve katalog güncellemeleri genel outbox bütçesini kullanıyor: 3 deneme, 5 sn ve 10 sn
  bekleme. Yaklaşık 15 saniyelik bir sağlayıcı kesintisi güncellemeyi ölü kuyruğa düşürüyor ve bir kişinin elle
  yeniden göndermesini gerektiriyor.
- Sağlayıcı istemcisi önbellekteki erişim anahtarını 401 aldığında silmiyor. Anahtar süresinden önce geçersiz
  kalırsa, süresi dolana kadar her çağrı 401 alıyor.
- Restoran iptali mutfak fişlerini sipariş transaction'ının dışında kaydediyor. Stok kararı ile mutfak iptali
  arasında mutfakta başlayan bir hazırlık, kararın görmediği bir durum bırakabiliyor. Dış transaction geri
  alınırsa mutfak kalemleri iptal edilmiş ama sipariş açık kalıyor.
- Online siparişler hiç `Completed` olmuyor: teslimden sonra sonsuza kadar `Served` kalıyor. Kapanmış sipariş
  notlarının saklama süresi sonunda anonimleştirilmesi (`Completed/Cancelled/Rejected`) bu siparişlere hiç
  uygulanmıyor.
- Sağlayıcının kalem fiyatı iç katalog fiyatıyla hiç karşılaştırılmıyor.
- Teslim uç noktası, teslimat türü bilinmeyen bir sipariş için "başka bir işlem tarafından değiştirildi" diyor.
  Stok hataları ve eski sürüm hataları 500 dönüyor.

## Owned surface

- `plan/v1.2/online-ordering/V12-RMD-008-online-status-sync-correctness.md`
- `evidence/V12-RMD-008/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/BuildingBlocks/Messaging/ (V0-ARC-003) — olay türüne göre deneme profili; varsayılan davranış değişmez.
  - src/Host/Outbox/ ve src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs — outbox deposunun
    tek fabrikadan, profillerle kurulması.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs ve src/Modules/OnlineOrdering/Yemeksepeti/StatusSync/
    (V12-ONL-003) — sağlayıcı olaylarının deneme profili, 401'de anahtarın silinmesi.
  - src/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/ (V12-ONL-002) — katalog fiyatının satıra
    taşınması.
  - src/Host/Experience/OnlineOrdering/ — iptalde mutfak fişlerinin aynı transaction'da kilitlenip kaydedilmesi,
    teslimde `Completed`, fiyat farkı kaydı, uç nokta hata eşlemesi.
  - src/Modules/Reconciliation/OnlineOrders/ (V12-REC-001) — `ProviderPriceMismatch` kaynak çifti.
  - src/Clients/PosTerminal/src/features/online-operations/ — yeni fark türünün Türkçe etiketi.
  - tests/BuildingBlocks/Messaging/ tests/Modules/OnlineOrdering/Yemeksepeti/StatusSync/
    tests/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/ tests/Modules/Reconciliation/OnlineOrders/
    tests/Host/Experience/OnlineOrdering/ ve tests/Host/Experience/Reconciliation/ — testler ve fikstürler.

## In scope

1. `OutboxRetryProfile(EventType, MaxAttempts, BaseDelay, MaxDelay)`: profili olan olay türü kendi bütçesini
   kullanır. Profili olmayan her olay eskisi gibi 3 deneme ve 5 sn tabanla çalışır. Yemeksepeti durum ve katalog
   olayları 12 deneme, 30 sn taban ve en çok 30 dk bekleme alır; ölü kuyruğa düşmeden önce yaklaşık 3 saat
   denenir. Ölü güncelleme mevcut `LocallyAcceptedProviderUnknown` vakasıyla görünür kalır.
2. `YemeksepetiPartnerHttpClient`: 401 yanıtında, istekte kullanılan anahtar hâlâ önbellekteyse silinir. Bir
   sonraki deneme yeni anahtar alır.
3. `CancelLocallyAsync`: siparişin mutfak fişleri stok kararından önce aynı transaction'da kilitlenir
   (`FOR UPDATE`, V1-RMD-313) ve aynı transaction'da kaydedilir. Mutfaktaki her yazma önce fiş satırını
   güncellediği için, karar ile iptal arasında hazırlık başlayamaz. Transaction geri alınırsa mutfak iptali de
   geri alınır. `PostgresKitchenItemStateProvider`'a `FOR SHARE` eklenmez: fiş kilidi aynı korumayı modül
   sınırını aşmadan sağlar.
4. Teslim (`HandOverAsync`) siparişi `Served` üzerinden `Completed` durumuna taşır; ödeme platformdadır ve
   restoranın işi teslimde biter. Sağlayıcının sonradan bildirdiği iptal, eskisi gibi `Diverged` kanıtı olur.
   Canlı veri olmadığı için (V0-YSP-001 `Blocked`) geriye dönük migration yazılmaz.
5. Normalizer her satıra katalogdaki güncel fiyatı (`CatalogPrice`) ekler. Alımda, farklı fiyatlı satırlar
   `priceDifferences` olarak ve `pricesMatch` alanıyla kaydedilir. Sipariş durdurulmaz. `ProviderPriceMismatch`
   kaynak çifti vakayı açar; tutar Σ|sağlayıcı − katalog| × adet, çözümü elle yapılır.
6. Uç nokta hataları: teslimat türü bilinmeyen teslim → 409 `HANDOVER_NOT_SUPPORTED`. Stok yapılandırma ve
   yetersiz stok hataları → 409 `STOCK_NOT_AVAILABLE`. `StaleOrderRowVersionException` → 409
   `CONCURRENCY_CONFLICT`. Hepsinin mesajı Türkçedir.

## Out of scope

- Sağlayıcının gerçek davranışı (V0-YSP-001 `Blocked`); istemci taslak olarak kalır.
- V12-RMD-006 kapsamındaki mutabakat ve arayüz bulguları.

## Dependencies

- V12-RMD-005

## Deliverables

- Kod ve testler.

## Acceptance evidence

- İlgili test projeleri yeşil; mutasyon kontrolü `evidence/V12-RMD-008/` altında.
- `task_scope_tool.py --task-id V12-RMD-008 --diff-base <InProgress commit>` exit 0.

## Handoff

- None

# V1-RMD-009 - SQL persistence, migration dependency ordering, quantity invariants and catalog pagination

- Task ID: V1-RMD-009
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Goal

012 btree_gist migrasyon bağımlılık sırasını düzeltmek, kümülatif sipariş kalemi miktar sınırını (maks 999) doğrulamak,
katalog sorgusuna bounded sayfalama/filtreleme eklemek ve cashier siparişini seçilen masaya transaction içinde bağlamak.

## Owned surface

- `src/Host/DualScreen/DualScreenContracts.cs`
- `database/migrations/V1/V1-CAT-002/007-catalog-pricing.up.sql`
- `database/migrations/V1/V1-CAT-002/007-catalog-pricing.down.sql`
- `database/migrations/V1/V1-FND-021/012-btree-gist-ownership.up.sql`
- `database/migrations/V1/V1-FND-021/012-btree-gist-ownership.down.sql`
- `tests/Host/MigrationComposition/PostgresqlExtensionLifecycleTests.cs`
- `evidence/V1-RMD-009/**`
- PO:2026-08-28 deep code audit kararıyla DualScreenStore.cs ve DualScreenStoreTests.cs yüzeyi V1-RMD-035'e devredildi; bu historical task closed kalır.
- PO:2026-08-28 additive floor migration kararıyla migration order ve ManifestTests.cs yüzeyi V1-RMD-026'ya
  devredildi; bu historical task closed kalır.
- PO:2026-08-28 `RMD032-F001` kararıyla `DualScreenHostTests.cs` runtime kitchen station contract doğrulaması için
  V1-RMD-034'e devredildi; bu historical task closed kalır.

## In scope

- Kullanıcı onaylı exact historical lifecycle correction ile boş kurulum, forward/down/re-forward ve daha önce 007 ile
  012 uygulanmış veritabanı geçmişinin uyumluluğunu koruyarak migration dependency sırasını düzeltmek.
- `DualScreenStore.AddItemAsync` içinde kümülatif miktar kontrolü eklemek.
- `GetCatalogAsync` için kategori filtreleme, limit ve bounded sayfalama sözleşmesi tanımlamak.
- `StartOrderRequest` ile geriye uyumlu, isteğe bağlı `tableId` ve beklenen masa satır sürümünü kabul etmek; masa satırı
  kilidi, `orders.orders.table_id`, `table_mgmt.tables.current_order_id`, durum ve `row_version` güncellemelerini aynı
  transaction içinde gerçekleştirmek. Çakışma, re-entry ve geçersiz masa durumları kısmi yazım bırakmadan reddedilir.
- Boş PostgreSQL 18 üzerinde forward, down ve re-forward migrasyon testlerini çalıştırmak.

## Out of scope

- UI bileşenlerini değiştirmek.
- Yeni iş modülü şeması eklemek.

## Dependencies

- V1-GOV-006
- V1-GOV-007
- V1-GOV-008
- V1-RMD-008

## Deliverables

- Tersinir PostgreSQL 18 migrasyon sırası.
- Kümülatif miktar korumalı ve bounded katalog sorgulu veri erişim katmanı.
- Table-aware order contract ve atomic table/order pointer consistency.
- Migrasyon ve eşzamanlılık doğrulama testleri.

## Acceptance evidence

- Digest-pinned boş PostgreSQL 18 üzerinde 001..son, son..001 ve yeniden ileri migrasyon gerçek exit code 0 verir;
  rollback hiçbir `CASCADE` veya sahip olunmayan nesne kaybı kullanmaz. Daha önce 007 ve 012 uygulanmış history
  fixture'ı yeni lifecycle modeliyle forward/down/re-forward edildiğinde migration identity ve extension/constraint
  sahipliği korunur.
- Exact historical dosya düzeltmesi yalnız lifecycle dependency kusurunu giderir; implementer çözümü temiz PostgreSQL
  18 ve existing-history fixture kanıtıyla seçer, acceptance metni belirli bir SQL workaround'u dikte etmez.
- Aynı order/product için concurrent add ve sequential add yollarında kümülatif miktar 999'u aşamaz; DB transaction
  isolation/lock davranışı ve error contract test edilir.
- Catalog contract kategori filtresi, deterministic cursor/page, bounded limit ve backward compatibility içerir;
  yalnız `LIMIT 1000` eklemek sayfalama kabulü değildir. Production HTTP endpoint'e karşı gerçek integration testleri
  parametresiz varsayılan katalog çağrısını, kategori filtresini, deterministic cursor devamını ve bounded limit
  reddetme/sınırlama davranışını doğrular. Temsilî büyük veriyle `EXPLAIN ANALYZE` ölçümü kaydedilir.
- Table-aware order contract testi, cashier'in seçtiği masaya yeni order'ı bağlar; aynı transaction sonunda
  `orders.orders.table_id`, `table_mgmt.tables.current_order_id`, `Occupied` durumu ve artırılmış `row_version`
  birlikte gözlemlenir. Mevcut aktif order, `Reserved`/`Cleaning`/`OutOfService` masa, beklenen sürüm çakışması ve
  transaction rollback yolları gerçek PostgreSQL üzerinde doğrulanır; body'siz eski çağrı `table_id = NULL` ile
  geriye uyumlu kalır.
- `dotnet test ALKAROS.slnx --configuration Release` tüm veritabanı testleriyle birlikte exit code 0 verir.
- `evidence/V1-RMD-009/**` altında PostgreSQL 18 migrasyon logları kaydedilir.

## Handoff

- V1-RMD-010

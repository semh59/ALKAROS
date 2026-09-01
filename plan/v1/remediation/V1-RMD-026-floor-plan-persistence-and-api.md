# V1-RMD-026 - Floor-plan persistence and API

- Task ID: V1-RMD-026
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Goal

Mevcut masa yaşam döngüsü, taşıma, birleştirme, ayırma ve rezervasyon concurrency kurallarını zayıflatmadan yetkili
salon tuvali, masa geometrisi, şekil ve kalıcı sandalye verisini saklamak; versioned salon planı okuma/yazma
sözleşmelerini açmak.

## Owned surface

- `database/migrations/V1/V1-RMD-026/**`
- PO:2026-08-31 kararıyla database/MigrationComposition/order.json ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs yüzeyleri V1-GOV-040'a devredildi; bu historical task closed kalır.
- `src/Modules/Tables/FloorPlan/**`
- `src/Modules/Tables/TableLifecycle/Zone.cs`
- `src/Modules/Tables/TableLifecycle/TableRepository.cs`
- `src/Modules/Tables/TableLifecycle/PostgresTableRepository.cs`
- `src/Modules/Tables/TableLifecycle/PostgresZoneRepository.cs`
- `src/Modules/Tables/TableLifecycle/TablesModule.cs`
- PO:2026-08-28 deep code audit kararıyla Table.cs yüzeyi V1-RMD-035'e devredildi; bu historical task closed kalır.
- `src/Host/Experience/Tables/TableManagementApplication.cs`
- `src/Host/Experience/Tables/TableManagementContracts.cs`
- `src/Host/Experience/Tables/TableManagementStore.cs`
- `tests/Modules/Tables/TableLifecycle/FloorPlan*.cs`
- `tests/Host/Experience/Tables/FloorPlan*.cs`
- `evidence/V1-RMD-026/**`

## Dependencies

- V1-GOV-019
- V1-RMD-009

## Acceptance evidence

- Eklemeli PostgreSQL migration; sınırlı salon tuvalini, masa x/y/genişlik/yükseklik/şekil/dönüş değerlerini, kalıcı
  sandalyeleri ve row version'ları tanımlar. Mevcut migration dosyaları değişmez; boş veritabanında up/down/up ve
  manifest sırası geçer.
- Kurulum kaydı; sınırları, desteklenen şekil/dönüşü, benzersiz sandalye numarasını, kapasite/sandalye uyuşmazlığı
  bildirimini ve birleştirme dışı çakışmayı doğrular; transaction atomiktir ve stale version kısmi geometri değişikliği
  bırakmadan `409` döndürür.
- Yetkili API; authoritative geometriyi, sandalyeleri, masa pointer'larını, birleştirme/rezervasyon bağlamını ve izinli
  komutları döndürür; eksik session `401`, eksik izin `403` olur ve DTO'lar müşteri ya da secret verisi içermez.
- Release build ile masa/domain/HTTP/migration testleri exit code `0` verir. Gerçek PostgreSQL senaryosu salon oluşturur,
  masa ve sandalyeleri konumlandırır, çakışmayı reddeder, conflict üretir ve process restart sonrasında veriyi korur.

## Handoff

- V1-RMD-027

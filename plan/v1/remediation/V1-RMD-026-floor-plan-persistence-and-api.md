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
- `database/MigrationComposition/order.json`
- `src/Modules/Tables/FloorPlan/**`
- `src/Modules/Tables/TableLifecycle/Table.cs`
- `src/Modules/Tables/TableLifecycle/Zone.cs`
- `src/Modules/Tables/TableLifecycle/TableRepository.cs`
- `src/Modules/Tables/TableLifecycle/PostgresTableRepository.cs`
- `src/Modules/Tables/TableLifecycle/PostgresZoneRepository.cs`
- `src/Modules/Tables/TableLifecycle/TablesModule.cs`
- `src/Host/Experience/Tables/TableManagementApplication.cs`
- `src/Host/Experience/Tables/TableManagementContracts.cs`
- `src/Host/Experience/Tables/TableManagementStore.cs`
- `tests/Modules/Tables/TableLifecycle/FloorPlan*.cs`
- `tests/Host/Experience/Tables/FloorPlan*.cs`
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`
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

# V1-RMD-133 - Independent audit: Production had zero HTTP surface, plus two competing "complete" paths

- Task ID: V1-RMD-133
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09) mimari bulgusu: Production modülü (parti
yaşam döngüsü + stok etkileri) tamamen gerçek, Postgres destekli, zaten
test edilmiş bir modüldü ve `ProductionModule` zaten
`ModuleRegistry.DefaultCatalog`'un bir parçasıydı — ama Host katmanında
hiçbir HTTP uç noktası yoktu. Bu görevi bağlarken bir kat daha derin,
daha dar bir bulgu ortaya çıktı: `IProductionBatchService
.CompleteBatchAsync` ve `IProductionStockEffectService
.ExecuteBatchStockEffectsAsync`, bir partiyi "tamamlamanın" birbirinden
habersiz, önceden var olan İKİ BAĞIMSIZ yoluydu. Birincisi yalnız
`ProductionBatch`'in kendi domain nesnesi üzerinden durumu çeviriyor
(hiç stok hareketi yok); ikincisi gerçek işi yapıyor (reçete
bileşenlerini tüketiyor, bitmiş porsiyon çıktısını kaydediyor, stok
yeterliliğini doğruluyor) ve AYNI durum sütununu ham SQL ile ayrıca
çeviriyor — daha zayıf bir ön koşulla (yalnız `Cancelled`'ı reddediyor,
"önce Başlatılmış olmalı" kuralını değil) ve yalnız parti zaten
tüketim/çıktı kaydına sahipse yakalayan bir idempotency korumasıyla.
İkisi de çağrılsaydı (art arda bile, eşzamanlı olmasa da) aralarında
hiçbir iyimser eşzamanlılık kontrolü olmadan aynı satırı iki kez
yazarlardı. Cashier client'ında da ayrı, bellek-içi
`ProductionBatchEngine` simülasyonu vardı — hiçbir sayfaya bağlı değildi
(aynı sınıf ölü kod). Kullanıcının kararı: gerçek bir yönetim API'si kur
ve ölü motoru sil.

## Owned surface

- `plan/v1/remediation/V1-RMD-133-production-management-experience.md`
  (yeni)
- `src/Host/Experience/Production/**` (yeni)
- `database/migrations/V1/V1-RMD-133/**` (yeni)
- `tests/Host/Experience/Production/**` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Clients/Cashier/Production/**, tests/Clients/Cashier/Production/**
    (V11-UI-002 sahipliğinde) — bu görevin kendi silme kararıyla tamamen
    kaldırıldı (`ProductionBatchEngine`/`ProductionBatchModels`, hiçbir
    sayfaya bağlı olmayan bellek-içi simülasyon, grep ile doğrulandı).
  - src/Host/DualScreen/DualScreenApplication.cs (ana Host kompozisyonu
    sahipliğinde) — `AddProductionManagementExperience()` ve
    `MapProductionManagement()` çağrıları eklendi.
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs (`PhaseBMax`),
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — migration 091 için standart 4 dosyalık desen (090 ile
    art arda, V1-RMD-132 aynı oturumda).
  - ALKAROS.slnx (paylaşılan) — yeni
    `ALKAROS.Host.Experience.Production.Tests` eklendi; silinen
    `ALKAROS.Cashier.Production.Tests` girişi kaldırıldı.

## In scope

1. **Parti CRUD/yaşam döngüsü** (`GET/POST /batches`, `GET /batches/{id}`,
   `GET /batches/by-number/{number}`, `POST .../start`, `POST .../cancel`,
   `PUT .../recipe-version`) — `IProductionBatchService`'in ince
   sarmalayıcısı; `CreatedBy` artık kimliği doğrulanmış yöneticiden geliyor.
2. **Tamamlama = TEK gerçek yol.** `POST /batches/{id}/complete` yalnız
   `IProductionStockEffectService.ExecuteBatchStockEffectsAsync`'i
   çağırıyor (gerçek envanteri hareket ettiren tek işlem) —
   `IProductionBatchService.CompleteBatchAsync` bu API'den hiç
   erişilebilir yapılmadı (kasıtlı: iki yolun ikisinin de erişilebilir
   olması, aralarında hiçbir koordinasyon olmadan aynı satırı yazma
   riskini yaratırdı). `ExecuteBatchStockEffectsAsync`'in eksik "önce
   Başlatılmış olmalı" ön koşulu, ne modüle dokunmadan, orkestrasyon
   katmanında (bu endpoint) geri getirildi — `batch.Status !=
   InProgress` ise `InvalidProductionBatchTransitionException`
   fırlatılıyor, `ProductionBatch.Complete()`'in kendi ön koşuluyla
   birebir aynı.
3. **Stok etkisi görünürlüğü** (`GET /batches/{id}/consumptions`,
   `GET /batches/{id}/outputs`) — `IProductionStockEffectService`'in
   zaten var olan okuma metotlarının ince sarmalayıcısı.
4. **Doğrulama: gerçek envanter etkisi.** Yeni HTTP testleri tam akışı
   çalıştırıyor (reçete + stok tohumla → parti oluştur → Başlatılmadan
   tamamlama denemesi reddedilir → başlat → tamamla) ve tamamlamanın
   reçete bileşenini GERÇEKTEN düşürüp çıktıyı GERÇEKTEN artırdığını,
   yetersiz stokta hiçbir etkinin kısmen bile uygulanmadığını, ve iptal
   edilmiş bir partinin başlatılamadığını doğruluyor.

## Out of scope

- `ProductionBatchService.CompleteBatchAsync`'in kendisi hâlâ var (kod
  tabanında, çağrılmadan) — bu görev onu silmedi, yalnız HTTP'den
  erişilebilir yapmadı; iki yolu birleştirmek veya birini silmek Production
  modülünün kendi sahipliğinde ayrı bir karar.
- Audit'in diğer mimari bulguları (#7 BuildingBlocks ölü kütüphaneleri) —
  ayrı, kullanıcıyla görüşülecek bir karar.
- `OperationsStatusEngine`/`CashierShellEngine` (aynı sınıf ölü kod,
  Production/Purchasing'le ilgisiz) — kullanıcı ayrıca bakacak.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Dört test grubu, gerçek Postgres'e karşı, aynı Docker imajıyla ayrı ayrı
  çalıştırıldı (boru hattı olmadan, gerçek `$?` yakalanarak): yeni
  `ALKAROS.Host.Experience.Production.Tests` (401/403, Başlatılmadan
  tamamlama reddi, tam yaşam döngüsü + gerçek reçete tüketimi/çıktı
  doğrulaması, yetersiz stokta kısmi etki olmadığı, iptal edilmiş
  partinin başlatılamaması): 4/4; Production'ın kendi mevcut modül
  testleri (bu görev modülün kaynak koduna hiç dokunmadı, regresyon
  olmadığını doğrulamak için): `ALKAROS.Production.BatchLifecycle.Tests`
  39/39, `ALKAROS.Production.StockEffects.Tests` 6/6; `ALKAROS.Host.Tests`
  (Manifest + Reachability + `ProductionExperienceCompositionTests`, yeni
  `AddProductionManagementExperience` kaydı dahil her modül servisini
  çözdüğünü doğruluyor) 121/121 — hepsi gerçek çıkış kodu `0`.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var
  olan ihlal (`ProductionBatchEngine.cs`'in silinmesiyle kendi ihlalleri
  V1-RMD-132 ile birlikte zaten gitmişti — bu görev sırasında değişmedi),
  yeni ihlal yok.

## Handoff

- None

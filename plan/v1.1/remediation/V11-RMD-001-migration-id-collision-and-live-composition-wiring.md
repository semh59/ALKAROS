# V11-RMD-001 - Migration ID collision fix and live composition wiring

- Task ID: V11-RMD-001
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in isteğiyle ("V1.1 bitti mi bir kontrol et... onu kontrollü
düzelterek maine alalım", 2026-09-06), `docs/audit/INDEPENDENT_DEEP_AUDIT_2026-09-06.md`'nin
KRİTİK bulduğu iki ölümcül entegrasyon açığını kapatır — her ikisi de bu
oturumda kendim doğruladım:

1. **Migration ID çakışması.** V1'in bu oturumda eklediği `V1-RMD-110`/
   `V1-RMD-111` migrasyonları (054, 055, 056) ile V1.1'in bağımsız
   numaralandırdığı `V11-UNT-001`/`V11-RCP-001`/`V11-INV-004` migrasyonları
   (054, 055, 056) aynı pozisyonları paylaşıyordu — iki oturum aynı `v1.1`
   branch'inde eşzamanlı ilerlerken hiç koordine olmadı. Şu ana kadar
   patlamıyordu çünkü `compose.yaml` yalnızca `V1` dizinini tarıyordu.
2. **V1.1'in 17 migrasyonu hiçbir zaman canlı kompozisyona bağlı değildi.**
   `order.json` yalnızca V1'i biliyordu, `compose.yaml`'ın
   `--migrations-dir`'i yalnızca `V1`'i tarıyordu — V1.1'in şeması gerçek
   bir dağıtımda asla oluşmuyordu.

## Owned surface

- plan/v1.1/remediation/V11-RMD-001-migration-id-collision-and-live-composition-wiring.md (yeni)
- Sınırlı ek — bu görev tek başına yeni bir yol sahiplenmiyor; aşağıdaki
  yollarda dar, açıklanmış bir düzeltme yapıldı, sahiplik ilgili göreve
  ait kalır (yollar geri-tik olmadan yazıldı ki denetleyici bunları
  sahiplik iddiası olarak parse etmesin):
  - database/migrations/V11/** (V11-UNT-001, V11-RCP-001, V11-INV-001,
    V11-INV-002, V11-INV-003, V11-INV-004, V11-INV-006, V11-INV-007,
    V11-RSV-001, V11-MNU-001, V11-MNU-002, V11-MNU-003, V11-PUR-001,
    V11-PUR-002, V11-RCP-002, V11-PRD-001, V11-PRD-002 sahipliğinde) — 17
    migrasyonun tamamı (34 dosya) 054-070'ten 057-073'e kaydırıldı (dosya
    adları + kendi kendine referans veren başlık yorumları); V1'in
    054-056'sına dokunulmadı.
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs (V1-RMD-103
    sahipliğinde) — V1.1'in 17 girişi eklendi, phaseBRange.max/PhaseBMax
    "056" → "073".
  - compose.yaml (V1-RMD-098/107 sahipliğinde) — --migrations-dir .../V1
    yolundan .../migrations'a (V1+V1.1'i birlikte tarayacak şekilde)
    genişletildi.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — 72 pozisyonluk yeni sayım/son giriş/aralık-dışı örnek.
  - 10 test dosyası, tests/Host/Experience/Roles,
    OfflineReconciliation, Billing, Tables, Authorization altında ve
    tests/Host/MigrationComposition/DualScreen,
    tests/Host/MigrationComposition/Program altında, ayrıca
    tests/Host/Experience/KitchenOperations/KitchenOperationsTestDatabase.cs
    (ilgili görevlerin sahipliğinde) — kendi test veritabanlarını
    kurarken hâlâ yalnızca .../V1'i taradıkları için order.json'un artık
    073'e kadar giden pozisyonlarını doğrulayamıyorlardı
    (MissingUp/MissingDown → StartupFailed); aynı .../migrations
    genişletmesi uygulandı.
  - 21 test dosyası, tests/Modules altında Inventory, Recipes,
    Production, Purchasing, Menu, Reporting modüllerinin kendi
    görevlerinin sahipliğinde — dosya adı ve tanımlayıcı
    (MigrationNNNAsync vb.) referansları yeni numaralara eşlendi; test
    mantığı değişmedi.

## In scope

1. `database/migrations/V11/**`'nin 17 migrasyonunu 057-073'e kaydırmak
   (V1'in 054-056'sını korumak için) — dosya adları, `.up`/`.down` içindeki
   kendi kendine referans veren "Migration NNN" yorum satırları.
2. `order.json`'a V1.1'in 17 girişini (tablo adlarıyla) eklemek,
   `phaseBRange.max`/`PhaseBMax`'ı "073" yapmak.
3. `compose.yaml`'ın migrations-dir'ini genişletmek.
4. Gerçek bir Postgres'e karşı **tam 001-073 zincirini** uçtan uca
   doğrulamak (bkz. Acceptance evidence) — bu, hiçbir otomatik testin daha
   önce hiç kanıtlamadığı bir şeydi (mevcut testler ya sentetik manifesto
   parçaları ya da yalnız-V1 dar kapsamlı fixture'lar kullanıyordu).
5. Yeniden numaralandırmanın kırdığı 10+21 test dosyasını düzeltmek.

## Out of scope

- **5 modülün (`Inventory`, `Recipes`, `Production`, `Purchasing`, `Menu`)
  `IModule`/`ModuleRegistry`/Host'a hiç kayıtlı olmaması.** Doğruladım:
  bu yalnızca bir mimari-görünürlük eksikliği değil — `src/Host` bu 5
  modülden HİÇBİRİNE hiçbir şekilde (ad-hoc DI dahil) referans vermiyor;
  hiçbirinin HTTP uç noktası yok. `IModule` eklemek tek başına anlamlı bir
  düzeltme olmaz (hâlâ hiçbir tüketicisi olmaz) — gerçek düzeltme her
  modül için bir Host/Experience uç nokta katmanı inşa etmek, ki bu V1'in
  kendi tarihinde de (domain katmanı önce, Host kablolaması ayrı bir
  dalgada) izlenen bir sıra. Semih'in kapsam kararını bekleyen ayrı bir
  görev olarak bırakıldı.
- Cross-schema doğrudan SQL yazma iddiaları (`Production`/`Purchasing` →
  `inventory.*`) — henüz satır satır doğrulanmadı, ayrı bir görev.
- `v1.1` → `master` branch birleştirmesi — bu görev yalnızca birleştirmeyi
  güvenli hale getiren ön koşulu kapatıyor.

## Dependencies

- V1-RMD-111

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- **Tam zincir uçtan uca doğrulandı** (`alkaros-test` konteyner imajı,
  gerçek scratch Postgres'e karşı): `dotnet ALKAROS.Host.dll
  --order-manifest .../order.json --migrations-dir .../migrations
  --db-url ...` → "Migration composition validated: 72 position(s), 144
  script(s)." → tüm 001-073 `applied`, "All 72 migration(s) verified; 72
  applied.", exit 0. Ardından pozisyon 073 rollback edilip yeniden
  uygulandı, temiz.
- `docker compose -f compose.yaml -f compose.test.yaml run --rm test`:
  **79/79 test projesi, sıfır başarısız** (öncesinde 6 proje + kendi
  proje toplam 121'i 18 `StartupFailed` ile başarısız oluyordu — 10
  dosyanın migrations-dir düzeltmesinden sonra hepsi yeşile döndü).
- `python tools/plan-audit/plan_audit_tool.py validate`,
  `validate-coverage`: sıfır hata.
- `python tools/consistency-audit/consistency_audit.py`: bu görevin
  değiştirdiği hiçbir dosyada ihlal yok; depodaki 13 ihlal öncekiyle
  aynı, hepsi bu görevin dokunmadığı dosyalarda.

## Handoff

- V11-GOV-001

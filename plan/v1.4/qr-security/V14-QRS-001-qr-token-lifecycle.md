# V14-QRS-001 - Implement QR token lifecycle

- Task ID: V14-QRS-001
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.34-I.37
- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21

## Goal

Reusable raw secret saklamadan hashed, revocable ve time/policy-bound Table token yayımlamak.

## Owned surface

- `src/Modules/QrOrdering/TokenLifecycle/**`, `tests/Modules/QrOrdering/TokenLifecycle/**`,
  `database/migrations/V14/V14-QRS-001/**`
- `src/Modules/QrOrdering/ALKAROS.QrOrdering.csproj` (yeni — modülün proje
  dosyası, `TokenLifecycle/` klasörünün bir üst dizininde).
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Host/Composition/Modules/ModuleRegistry.cs, src/Host/ALKAROS.Host.csproj
    (ilgili V1-FND/foundation görevlerinin sahipliğinde) — yalnız
    `QrOrderingModule`'ün `DefaultCatalog`/proje referansına eklenmesi.
  - ALKAROS.slnx (yalnız yeni iki proje kaydı için).
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs,
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004/V1-IAM-025 sahipliğinde)
    — yalnız migration 078 kaydı eklendi; önceki 077 kaydına dokunulmadı.
  - tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs (aynı foundation
    görevinin sahipliğinde) — `DefaultCatalog` sayısı 18'den 19'a güncellendi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Token hash, issuance, rotation, expiry, revocation ve Table binding.

## Out of scope

- Toplu aktarma taşımacılığı, müşteri siparişi UI ve table status.

## Dependencies

- V0-QRG-001

## Deliverables

- `src/Modules/QrOrdering/TokenLifecycle/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Veritabanı sızıntısı, kullanılabilir ham belirtecin bulunmadığını ortaya çıkarıyor; süresi dolmuş/iptal edilmiş
  belirteç başarısız olur; döndürme, yapılandırıldığı şekilde önceki belirteci geçersiz kılar.
- Uygulanan model: restoranın gerçek dolaşımına göre "yapılandırılmış" olan
  şey art niyet penceresi değil, geçiş biçimidir — döndürme her zaman
  atomik, sert bir kesim (eski belirteç aynı transaction'da hemen iptal
  olur; iki geçerli belirtecin aynı anda var olduğu bir ara durum
  yoktur), `ux_table_tokens_active_per_table` kısmi tekil indeksiyle
  veritabanı seviyesinde garanti edilir.
- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `dotnet test tests/Modules/QrOrdering/TokenLifecycle/ALKAROS.QrOrdering.TokenLifecycle.Tests.csproj`:
  gerçek Postgres'e karşı 21/21 test geçti — kapsam: ham belirtecin
  veritabanı sütununda hiç görünmediği (`ADatabaseLeakExposesNoUsableRawToken`),
  süresi dolmuş/iptal edilmiş/bilinmeyen belirtecin ayrı ayrı reddedildiği,
  eşzamanlı ilk-yayım yarışında yalnız birinin kazandığı
  (`ConcurrentIssuanceForTheSameTableLetsExactlyOneWin`, gerçek Postgres
  unique-violation'ı yakalayarak), döndürmenin atomik olduğu (asla sıfır
  ya da iki aktif satır) ve varsayılan ömrün tam 4 saat olduğu
  (`docs/architecture/qr-relay-topology.md` rule 2).
- Gerçek hata bulundu ve düzeltildi: ilk testte "süresi dolmuş" senaryosunu
  `expires_at`'i geçmişe (issued_at'ten önceye) çekerek simüle etmeye
  çalıştım — bu, `TableToken`'ın kendi constructor invariant'ını
  (`expiresAt > issuedAt`, satırın her okunuşunda yeniden uygulanır) ihlal
  ederek satırın okunmasını tamamen imkansız hâle getirdi (gerçek
  `ArgumentException`, testte yakalandı). Düzeltme: süresi issuance'tan
  1ms sonrasına ayarlamak (invariant korunur, gerçek zaman kısa sürede
  onu geride bırakır).
- `python tools/plan-audit/plan_audit_tool.py validate` ve
  `python tools/consistency-audit/consistency_audit.py`: sıfır yeni hata/ihlal.

## Handoff

- V14-QRS-002
- V14-QRO-001

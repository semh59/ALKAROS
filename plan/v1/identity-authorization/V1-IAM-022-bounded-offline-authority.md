# V1-IAM-022 - Bounded Offline Authority

- Task ID: V1-IAM-022
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

Sınırlı çevrimdışı yetki (karar dokümanı §5): Host, oturum başında cihaza kısa
ömürlü bir öz onay bütçesi verir. Bütçe, isteyenin kendi çevrimiçi `auto_within`
politikalarının izin bazında (tutar ile adet) alınmış tek seferlik anlık
görüntüsüdür ve birkaç saat sonra dolar; çevrimdışıyken yeni yetki yaratılmaz.
Çevrimdışı `grant` eylemleri, bütçe elverdikçe cihazda yerelde geçer; bağlantı
dönünce her biri `identity.authorization_grants` günlüğüne yazılır ve
`identity.offline_authority_replays` ile bütçesine bağlanır. Çevrimdışı yetki
canlı politikayla yeniden doğrulanır: bütçe dolmuşsa, hat aşılmışsa ya da canlı
politika artık reddediyorsa `denied` yazılır; aksi halde `pending` (bir replay
satırı taşıyan bekleyen grant = `offline_pending_review`) olarak yöneticiye
düşer. Oturum başında `IssueAsync`'in çağrılması ve reconnect ucunun
`ReconcileAsync`'e bağlanması `V1-IAM-024` entegrasyon kapsamındadır.

Tasarım sapması: karar dokümanı §5 bütçeyi cihazdaki imzalı bir JWT olarak
taslaklar; bu görev bunun yerine sunucuda tutulan bir yetki satırı
(`offline_authority_budgets` ile `offline_authority_budget_lines`, tahmin
edilemez `budget_id` anahtarı) kullanır. Gerekçe: kod tabanında HMAC/JWT imza
anahtarı altyapısı yoktur (oturum jetonları rastgele artı SHA-256'dır) ve yeni
bir anahtar yönetimi yüzeyi tasarlanmamış teknik borç olurdu. Sunucu satırı
tanım gereği taklit edilemez (istemci tavanı bildirmez, yalnızca `budget_id`
taşır) ve daha denetlenebilirdir; garanti aynıdır: sınırlı, oturum başına,
süreli, çevrimiçi tam uzlaştırma. Cihazın yerel kopyası yalnızca çevrimdışı
arayüz içindir.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-022-bounded-offline-authority.md`
- `database/migrations/V1/V1-IAM-022/**`
- `src/Modules/Identity/Authorization/Offline/**`
- `tests/Modules/Identity/Authorization/Offline/**`
- Yüzey devri (giriş): database/MigrationComposition/order.json ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs, migration 047 için V1-IAM-021'den bu göreve devredildi (PO:2026-09-04). src/Modules/Identity/IdentityModule.cs, yetkilendirme dalgasının DI kayıt evi olarak V1-IAM-021'den bu göreve devredildi (PO:2026-09-04).
- Yüzey devri (çıkış): database/MigrationComposition/order.json, tests/Host/MigrationComposition/Manifest/ManifestTests.cs (migration 048) ve src/Modules/Identity/IdentityModule.cs, bu görev kapandıktan sonra V1-IAM-023'e devredildi (PO:2026-09-04).
- src/Host/Composition/Migrations/MigrationManifest.cs içindeki PhaseBMax sabiti V1-FND-004 sahipliğinde kalır; bu görevde yalnızca faz üst sınırı 047 değerine güncellenmişti (V1-RMD-089/9. dalga deseni).
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-019

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18): `ALKAROS.Identity.Authorization.Tests`
  153/153 (yeni `Offline/**` 29 test — `OfflineAuthorityBudgetModelTests` saf
  (`IsExpiredAt` dahil sınır, `LineFor`, `Admits` adet-sonra-tutar,
  `LinesFor` yalnız rolün `auto_within` politikalarını süzer);
  `OfflineAuthorityBudgetMigrationTests` metin (üç tablo, `session_id` UNIQUE,
  pencere/limit/adet CHECK'leri, `_lines` -> budget CASCADE, `replays` ->
  grant/budget FK ve sıralama CHECK'i, grants tablosuna ALTER yok, seed yok);
  `PostgresOfflineAuthorityBudgetRepositoryTests` gerçek 005..047 zinciriyle
  (create + satırlar + iki arama, aynı oturum için yeniden verince eskisini
  silme, satırsız bütçe, geçersiz pencere reddi); `OfflineAuthorityBudgetServiceTests`
  (yalnız rolün `auto_within` politikalarını 4 saatlik bütçeye alır, açık
  TTL, politikasız rol -> satırsız, yeniden verme eskisini siler);
  `OfflineGrantReconcilerTests` + `OfflineAuthorityDownMigrationTests` (hat
  içinde -> `pending` grant + replay satırı; tutar aşımı -> `denied`; adet 1
  iken ikinci istek -> `denied`; bütçe dolduktan sonra alınan eylem ->
  `denied`; bütçede olmayan izin -> `denied`; canlı politika `always_deny` ->
  `denied`; aynı eylemi iki kez uzlaştırma idempotent; bilinmeyen `budget_id`
  -> `UnknownOfflineAuthorityBudgetException`; down üç çevrimdışı tabloyu
  düşürür, `authorization_grants` yerinde kalır)); `Host.Tests`
  `Manifest.ManifestTests` 16/16 (`PhaseBMax` 047, 46 pozisyon, son giriş
  tabloları `["offline_authority_budgets", "offline_authority_budget_lines",
  "offline_authority_replays"]`).
- Migration ileri: 001..047 zinciri boş `alkaros_fm5` veritabanına uygulandı;
  üç çevrimdışı tablo oluştu. Geri: `047-*.down.sql` uygulandı; üçü de düştü,
  `identity.authorization_grants` ile `identity.authorization_delegations`
  yerinde kaldı.
- Semih için gerçek senaryo: yönetici garson rolüne `bills.void` için
  `auto_within` (≤ ₺80, ≤ 1) tanımlar. Oturum başında garsonun cihazı
  `bills.void: {limit ₺80, count 1}` satırlı, 4 saatlik bir bütçe alır. Cihaz
  çevrimdışıyken garson ₺60'lık tek bir void yapar; ikinci void bütçe adedi
  bittiği için cihazda engellenir. Bağlantı dönünce ilk void
  `authorization_grants`'a `pending` + replay satırıyla yazılır ve yönetici
  incelemesine (`offline_pending_review`) düşer; bütçe süresi dolduktan sonra
  alınmış bir void'in tekrar oynatılması `denied` olur.

## Handoff

- V1-IAM-023

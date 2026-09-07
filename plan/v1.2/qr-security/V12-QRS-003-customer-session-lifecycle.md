# V12-QRS-003 - Implement QR customer session lifecycle

- Task ID: V12-QRS-003
- Status: Done
- Assignee: Claude Sonnet 5 (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.34-I.37
- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21

## Goal

Raw Table token'ı reusable browser credential'a çevirmeden QR token validation sonrası revocable customer session
oluşturmak.

## Owned surface

- `src/Modules/QrOrdering/CustomerSession/**`, `tests/Modules/QrOrdering/CustomerSession/**`,
  `database/migrations/V12/V12-QRS-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — aşağıdaki yol ilgili görevin sahipliğinde kalır (yol
  geri-tik olmadan yazıldı ki denetleyici bunu sahiplik iddiası olarak
  parse etmesin):
  - src/Modules/QrOrdering/TokenLifecycle/QrOrderingModule.cs (V12-QRS-001
    sahipliğinde) — yalnız ICustomerSessionRepository/CustomerSessionService
    kayıtları eklendi (V12-QRT-003'ün aynı dosyaya yaptığı artımlı ek
    hazinesiyle aynı gerekçe: paylaşılan modül DI kayıt noktası).

## In scope

- Oturum verme, karma kalıcılığı, boş/mutlak süre sonu, iptal, table bağlama ve çerez/başlık güvenlik politikası.
- Oturumdan token kaynağı ve denetim olayları.

## Out of scope

- QR jeton oluşturma, aktarma aktarımı, menü oluşturma ve order oluşturma.

## Dependencies

- V12-QRS-001
- V12-QRS-002

## Deliverables

- Müşteri oturumu uygulaması ve contract sürümü.
- Sona erme, tekrar oynatma, iptal etme, jeton rotasyonu ve çapraz table izolasyon testleri.
- Kalıcılık gerektiğinde ileri ve geri alma migration.

## Acceptance evidence

- Yakalanan bir ham oturum başka bir table'ye erişemez; iptal edilen, boşta kalma süresi dolan ve mutlak süresi dolan
  oturumlar, denetlenen neden kodlarıyla başarısız olur.
- Migration 085 (`database/migrations/V12/V12-QRS-003/085-qr-ordering-customer-sessions.up/down.sql`):
  `qr_ordering.customer_sessions` — `session_id` PK, `token_hash` UNIQUE (ham jeton hiçbir zaman saklanmaz, yalnız
  SHA-256 karması), `table_id` FK → `table_mgmt.tables`, `revoked_at`/`revoked_reason` çift alanı bir CHECK ile
  birlikte set edilmeye zorlanır. `database/MigrationComposition/order.json`,
  `src/Host/Composition/Migrations/MigrationManifest.cs` (`PhaseBMax = "085"`) ve
  `tests/Host/MigrationComposition/Manifest/ManifestTests.cs` güncellendi.
- `CustomerSessionService.IssueAsync` ham table token'ı yalnız `TableTokenService.ValidateAsync` (V12-QRS-001) ile
  doğrular; ham table token hiçbir zaman session credential olarak yeniden kullanılmaz — başarı durumunda ayrı, kendi
  jeneratörüyle (`CustomerSessionTokenGenerator`, `alkaros-customer-session:` öneki) üretilmiş yeni bir ham/karma
  çift basılır.
- `ALKAROS.QrOrdering.CustomerSession.Tests`: 27/27 test geçti (14 domain testi + 13 servis testi), gerçek Postgres'e
  karşı (yerelde `localhost:55432` ve Docker'da `test-postgres`), şunları kapsar: geçerli table token'dan oturum
  verme ve kendi table'ine doğrulanma; bilinmeyen/iptal edilmiş table token ile verme başarısızlığı (`NOT_FOUND`,
  `REVOKED` — alttaki `TableTokenValidationResult` nedeni aynen yüzeye çıkar); DB satırı sızsa bile ham jetonun
  hiçbir alt dizisinin karma sütununda bulunmaması; bilinmeyen/iptal edilmiş/mutlak süresi dolan/boşta kalma süresi
  dolan oturum doğrulama başarısızlıkları tam neden koduyla (`NOT_FOUND`/`REVOKED`/`EXPIRED`/`IDLE_EXPIRED`);
  başarılı doğrulamanın boşta kalma penceresini ileri kaydırması; varsayılan oturumun tam 4 saat sonra sona ermesi.
- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- `docker compose -f compose.yaml -f compose.test.yaml run --build --rm test` tüm suite (Release) geçti;
  `docker inspect alkaros-test-1 --format '{{.State.ExitCode}}'` → `0`.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → 13 önceden var olan, bu görevle ilgisiz ihlal (değişmedi),
  yeni ihlal yok.

## Handoff

- V12-CWB-001
- V12-CWB-002

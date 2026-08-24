# V1-RMD-006 - Implement production dual-screen POS vertical slice

- Task ID: V1-RMD-006
- Status: Planned
- Assignee: Unassigned
- Work type: integration
- Surface state: Existing

## Goal

V1-RMD-005 kararını çalışan bir production vertical slice'a dönüştürmek: cashier ekranındaki gerçek PostgreSQL
sipariş değişiklikleri bağımsız ve salt okunur müşteri ekranına versioned snapshot ile yansır; restart, reconnect,
yetki ve stale durumları fail-closed çalışır.

## Owned surface

- `src/Host/Program.cs`
- `src/Host/ALKAROS.Host.csproj`
- `src/Host/packages.lock.json`
- `src/Host/DualScreen/**`
- `src/Clients/PosTerminal/**`
- `tests/Host/MigrationComposition/DualScreen/**`
- `database/migrations/V1/V1-RMD-006/**`
- `database/MigrationComposition/order.json`
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`
- `evidence/V1-RMD-006/**`

## In scope

- Mevcut migration CLI davranışını koruyarak ASP.NET Core dual-screen serve modunu eklemek.
- PostgreSQL üzerinde terminal/display kaydı, kısa ömürlü pairing request, hash'li display session ve aktif order
  bağını forward/down migration ile persist etmek.
- Cashier için gerçek catalog okuma, Draft order açma, version-controlled satır ekleme/miktar değiştirme/silme ve
  submit endpoint'leri; müşteri ekranı için allowlist DTO üreten salt-okunur versioned snapshot endpoint'i.
- Commit sonrasında SignalR invalidation, reconnect'te tam snapshot ve en geç beş saniyelik HTTP reconciliation.
- Ayrı browser storage sınırlarında çalışan React + TypeScript cashier ve customer-display route'ları.
- Pairing, session revocation, yanlış terminal/display erişimi, stale snapshot ve server restart negatif yolları.

## Out of scope

- `src/Clients/WebPrototype/**` mock yüzeyi veya historical V1 task production dosyalarını değiştirmek.
- Payment provider, mali cihaz, fiziksel yazıcı, installer/watchdog veya production deployment davranışı.
- Gerçek müşteri/para ile go-live; bu görev V20 release gate'lerinin yerine geçmez.

## Dependencies

- V1-FND-026
- V1-RMD-001
- V1-RMD-002
- V1-RMD-005
- V1-ORD-002
- V1-IAM-003
- V1-SEC-002
- V1-OBS-001

## Acceptance evidence

- `dotnet restore ALKAROS.slnx`, `dotnet build ALKAROS.slnx --no-restore` ve dual-screen focused testleri exit code
  `0` verir; Node dependency install, TypeScript check, production build ve client testleri de exit code `0` verir.
- `038` migration çifti boş PostgreSQL 18 veritabanında forward/down/forward uygulanır; manifest testi yeni exact entry
  sayısını ve tablolarını doğrular.
- Cashier gerçek catalog ürünüyle sipariş açar; satır ekleme, miktar değiştirme, silme ve submit her seferinde
  PostgreSQL row version'ını artırır ve ikinci browser müşteri ekranı yeni snapshot'ı gösterir.
- Display principal mutation endpoint'lerinde `403`, başka terminal snapshot'ında `403`, expired/revoked session'da
  `401` alır; snapshot personel, token, provider, mali cihaz veya dahili not alanı taşımaz.
- SignalR kesildiğinde beş saniyelik HTTP reconciliation doğru revision'ı getirir; server/client restart sonrasında
  açık sipariş browser belleğinden değil PostgreSQL'den yeniden yüklenir.
- On saniye geçerli snapshot alınamazsa müşteri ekranı eski satır ve tutarları temizleyerek stale görünümüne geçer.
- `python -B tools/plan-audit/plan_audit_tool.py validate`, markdownlint, `git diff --check` ve pre-Done task-scope
  kontrolü exit code `0` verir.
- Semih iki fiziksel veya iki bağımsız browser penceresinde pairing, sipariş açma, satır değişimi, reconnect, restart,
  submit ve session revoke senaryolarını elle doğrulayabilir.

## Handoff

- V20-INS-001
- V20-SEC-001
- V20-UAT-001
- V20-REL-002

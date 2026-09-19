# V13-HUG-005 - Token terminal credential registration screen

- Task ID: V13-HUG-005
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C98
- PO:2026-09-18

## Goal

`V13-HUG-001..004` (kart ödeme, uzlaşma) ve `V13-FSC-004` her isteğinde
`terminal-id`/`branch-id`/`merchant-id` header'larına ve bir `client-id`/
`client-secret` çiftine ihtiyaç duyuyor (bkz. `evidence/v0/integrations/
V0-HUG-001/tokenx-documentation.postman_collection.json`). Bu değerlerin
NEREYE kaydedileceğini/yönetileceğini sahiplenen hiçbir görev yoktu — bu,
2026-09-18'de Semih'le yapılan görüşmede bulunan bir plan boşluğuydu
(bkz. memory `kasa-payments-token-audit-chain`). `terminal-id`/
`merchant-id`/`branch-id` fiziksel cihazın arkasında (AV/AT seri no) veya
TokenX Connect uygulamasının QR kodunda (`merchantId_branchId_terminalId`
formatında) bulunuyor; bu görev, bir yöneticinin bunları ve `client-id`/
`client-secret`'ı arayüzden girip kaydedebileceği bir ekran sağlar —
`V12-QRT-003`'ün (`RelaySettings`) Cloudflare API token'ı için kurduğu
AYNI desenle: `client-secret` AES-256-GCM zarfıyla şifrelenir, asla geri
okunmaz; `merchant-id`/`branch-id`/`terminal-id`/`client-id` gizli
olmadığı için düz metin saklanır ve durum sorgusunda geri döner.

**Not:** Bu görev Token'ın CANLI API'sine hiç istek atmaz — sadece
credential'ları depolar. Bu yüzden `V0-HUG-001`'e (Blocked) bağımlı
DEĞİLDİR ve `plan/TASK_STANDARD.md:57`/`:97` kuralına takılmadan normal
yoldan `InProgress`/`Done` olabilir.

## Owned surface

- `src/Modules/Payments/Token/TerminalCredential/**`
- `src/Host/Experience/TokenTerminalSettings/**`
- `src/Clients/PosTerminal/src/routes/TokenTerminalSettings.tsx`
- `src/Clients/PosTerminal/src/routes/TokenTerminalSettings.test.tsx`
- `database/migrations/V13/V13-HUG-005/**`
- `tests/Modules/Payments/Token/TerminalCredential/**`
- `tests/Host/Experience/TokenTerminalSettings/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
  (V1-IAM-025 ailesinin sahipliğinde kalır) — yalnız `127` girdisi eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
  (V1-FND-004 sahipliğinde kalır) — yalnız `PhaseBMax` `"126"` → `"127"`
  değişir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/api.ts,
  contracts.ts, App.tsx (birikimli olarak birçok görevin sahipliğinde,
  bkz. `V12-QRT-003`'ün kendi notu) — yalnız yeni
  `tokenTerminalCredentialStatus`/`saveTokenTerminalCredential` çağrıları
  ve `/settings/token-terminal` rotası eklenir, mevcut fonksiyonlar
  değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (V1-IAM-024 sahipliğinde kalır, `V12-QRT-003`'ün de aynı şekilde
  eklediği yer) — yalnız `AddTokenTerminalSettingsExperience`/
  `MapTokenTerminalSettingsApi` çağrıları eklenir.

## In scope

- `ITokenTerminalCredentialStore`/`PostgresTokenTerminalCredentialStore`:
  `client-secret`'ı `ALKAROS.SensitiveData` zarfıyla şifreler
  (`PostgresRelayCredentialStore`'un birebir aynı deseni); `client-id`/
  `merchant-id`/`branch-id`/`terminal-id` düz metin.
- `POST`/`GET /api/v1/terminals/{terminalId}/token-credential[/status]` —
  `RelaySettingsEndpoints`'in aynı auth/exception-filter desenini
  (`ApplicationPermissions.IntegrationsManage`, cashier cookie/Bearer)
  kullanır.
- `TokenTerminalSettings.tsx` — `RelaySettings.tsx`'in aynı ekran
  iskeletini (giriş, durum, form) kullanır; `/settings/token-terminal`
  rotası.
- Migration 127: `payments.token_terminal_credentials` tablosu.
- Gerçek Postgres'e karşı HTTP testleri (kaydet → durumu oku → değeri asla
  geri dönmediğini doğrula).

## Out of scope

- Token'ın canlı API'sine herhangi bir çağrı — bu ekran sadece credential
  depoluyor, `V13-HUG-001..004`'ün kendi işi.
- Birden fazla terminal/şube desteği — ALKAROS tek-kiracılı (bir Host =
  bir işletme, `[[v12-qrt-005-connector-container-isolation]]`'daki
  gibi doğrulanmış); tek bir credential seti yeterli (aynı
  `relay_credentials`'ın `credential_key` tekilliği deseni).

## Dependencies

- None

## Deliverables

- Yukarıdaki Owned surface'ın production code + migration + testleri.

## Acceptance evidence

- Gerçek Postgres'e karşı HTTP testleri: credential kaydedilir, durum
  sorgusu `configured: true` + `merchantId`/`branchId`/`terminalId`/
  `clientId` döner ama `clientSecret` asla dönmez; yetkisiz kullanıcı
  403 alır.
- `dotnet build`/`dotnet test` 0 hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → temiz; iki yeni
  test `.csproj`'u (`tests/Modules/Payments/Token/TerminalCredential/**`,
  `tests/Host/Experience/TokenTerminalSettings/**`) `ALKAROS.slnx`'e
  eklenmiş olmalı, üretim tarafında (`src/**`) yeni bir `.csproj` yok —
  değişen şey yalnızca var olan `ALKAROS.Payments`/`ALKAROS.Host`
  projelerine dosya eklenmesi.

## Handoff

- V13-HUG-001
- V13-HUG-002
- V13-HUG-003
- V13-HUG-004
- V13-FSC-004

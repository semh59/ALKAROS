# V14-QNB-006 - QNB e-Fatura credential registration screen

- Task ID: V14-QNB-006
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C21
- PO:2026-09-18

## Goal

`V14-QNB-001`/`V14-QNB-002` her çağrıda QNB `userId`/`password` (SOAP
oturum girişi) ve `vergiTcKimlikNo` (VKN — belge gönderiminde zorunlu
alan) ihtiyaç duyuyor (bkz. `evidence/v0/integrations/V0-QNB-001/**`).
`V13-HUG-005`'in Token için yaptığının aynısı: bir yöneticinin bu
credential'ları arayüzden girip kaydedebileceği bir ekran — `password`
AES-256-GCM zarfıyla şifrelenir, asla geri okunmaz; `userId`/
`vergiTcKimlikNo` gizli olmadığı için düz metin saklanır ve durum
sorgusunda geri döner.

**Not:** Bu görev QNB'nin CANLI API'sine hiç istek atmaz — sadece
credential'ları depolar. Bu yüzden `V0-QNB-001`'e (Blocked) bağımlı
DEĞİLDİR ve `plan/TASK_STANDARD.md:57`/`:97` kuralına takılmadan normal
yoldan `InProgress`/`Done` olabilir (`V13-HUG-005` ile birebir aynı
gerekçe).

## Owned surface

- `src/Modules/Invoicing/Qnb/CredentialRegistration/**`
- `src/Host/Experience/QnbCredentialSettings/**`
- `src/Clients/PosTerminal/src/routes/QnbCredentialSettings.tsx`
- `src/Clients/PosTerminal/src/routes/QnbCredentialSettings.test.tsx`
- `database/migrations/V14/V14-QNB-006/**`
- `tests/Modules/Invoicing/Qnb/CredentialRegistration/**`
- `tests/Host/Experience/QnbCredentialSettings/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
  (V1-IAM-025 ailesinin sahipliğinde kalır) — yalnız `128` girdisi eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
  (V1-FND-004 sahipliğinde kalır) — yalnız `PhaseBMax` `"127"` → `"128"`
  değişir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/api.ts,
  contracts.ts, App.tsx (birikimli olarak birçok görevin sahipliğinde) —
  yalnız yeni `qnbCredentialStatus`/`saveQnbCredential` çağrıları ve
  `/settings/qnb-credential` rotası eklenir, mevcut fonksiyonlar
  değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (V1-IAM-024 sahipliğinde kalır) — yalnız
  `AddQnbCredentialSettingsExperience`/`MapQnbCredentialSettingsApi`
  çağrıları eklenir.

## In scope

- `IQnbCredentialStore`/`PostgresQnbCredentialStore`: `password`'u
  `ALKAROS.SensitiveData` zarfıyla şifreler (`PostgresTokenTerminalCredentialStore`'un
  birebir aynı deseni — kendi private resolver/cipher/protector zinciri,
  paylaşılan DI singleton'ına dokunmaz); `userId`/`vergiTcKimlikNo` düz
  metin.
- `POST`/`GET /api/v1/terminals/{terminalId}/qnb-credential[/status]` —
  `TokenTerminalSettingsEndpoints`'in aynı auth/exception-filter
  desenini kullanır.
- `QnbCredentialSettings.tsx` — `TokenTerminalSettings.tsx`'in aynı
  ekran iskeleti; `/settings/qnb-credential` rotası.
- Migration: `invoicing.qnb_credentials` tablosu.
- Gerçek Postgres'e karşı HTTP testleri + vitest.

## Out of scope

- QNB'nin canlı API'sine herhangi bir çağrı.
- Birden fazla VKN/işletme desteği — tek-kiracılı model (V13-HUG-005 ile
  aynı gerekçe).

## Dependencies

- None

## Deliverables

- Yukarıdaki Owned surface'ın production code + migration + testleri.

## Acceptance evidence

- Gerçek Postgres'e karşı HTTP testleri: credential kaydedilir, durum
  sorgusu `configured: true` + `userId`/`vergiTcKimlikNo` döner ama
  `password` asla dönmez; yetkisiz kullanıcı 403 alır.
- `dotnet build`/`dotnet test` 0 hata; `pnpm typecheck`/`vitest` 0 hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → temiz.
- Gerçek tarayıcıda (Playwright/manuel) görsel doğrulama: giriş ekranı,
  form, kaydet sonrası durum güncellemesi.

## Handoff

- V14-QNB-001
- V14-QNB-002

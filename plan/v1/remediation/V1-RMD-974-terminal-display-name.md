# V1-RMD-974 - Terminale yönetici tarafından ad verilebilir

- Task ID: V1-RMD-974
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Yönetici her terminale bir ad verebilmeli (ör. Kasa 1). Ad Terminaller ekranında düzenlenir, oturum bilgisinde döner ve Kasiyer ekranı başlığında ile üst çubuktaki kimlikte kısa kimlik yerine görünür; ad yoksa kısa kimlik gösterilmeye devam eder.

## Owned surface

- `evidence/V1-RMD-974/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - database/migrations/V1/V1-RMD-974/226-terminal-display-name.up.sql
  - database/migrations/V1/V1-RMD-974/226-terminal-display-name.down.sql
  - database/MigrationComposition/order.json
  - src/Host/Composition/Migrations/MigrationManifest.cs
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs
  - src/Host/DualScreen/DualScreenStore.TerminalKey.cs
  - src/Host/DualScreen/DualScreenApplication.Endpoints.cs
  - src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs
  - tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs
  - tests/Architecture/ApiConventions/idempotency_allowlist.json
  - src/Clients/PosTerminal/src/features/management-security-system/TerminalsPanel.tsx
  - src/Clients/PosTerminal/src/features/management-security-system/TerminalsPanel.test.tsx
  - src/Clients/PosTerminal/src/features/management-security-system/api.ts
  - src/Clients/PosTerminal/src/features/management-security-system/models.ts
  - src/Clients/PosTerminal/src/features/management-security-system/SecuritySection.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-973

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

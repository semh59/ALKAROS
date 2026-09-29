# V1-RMD-455 - Ana Kasa ekranının üst çubuğuna Yönetim bağlantısı

- Task ID: V1-RMD-455
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-29

## Goal

V1-RMD-445'in gerçek Host ve veritabanıyla yapılan denemesinde Yönetim alanına ana Kasa ekranından ulaşılamadığı
görüldü: üst çubuktaki bağlantılar (Bekleyen hesaplar, Masalar, Mutfak, Menü) Yönetim'i içermiyordu; sekme yalnız başka
bir çalışma alanına geçildikten sonra kenar menüde çıkıyordu. `reports.view` yetkisi olan oturum için üst çubuğa
"Yönetim" bağlantısı eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-455-management-home-link.md`
- `src/Clients/PosTerminal/src/routes/Cashier.management-link.test.tsx`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx - yalnız üst çubuktaki Yönetim bağlantısı
- Sınırlı ek (paylaşılan, geri-tik olmadan):
  plan/AUDIT_MANIFEST.json
  plan/AUDIT_REPORT.md

## In scope

- Üst çubukta yalnız `reports.view` yetkisi olan oturuma görünen, `/management` adresine giden tek bağlantı ve testi.

## Out of scope

- Yönetim alanının kendisi (V1-RMD-445) ve diğer bölümler; Cashier.tsx'te başka değişiklik.

## Dependencies

- V1-RMD-445

## Acceptance evidence

- PosTerminal `pnpm run typecheck`, `pnpm run lint` ve `pnpm test` exit code 0; testler bağlantının yetkili oturumda
  göründüğünü ve yetkisiz oturumda görünmediğini doğrular. Çıktılar `evidence/V1-RMD-455/` altındadır.
- Gerçek Host ve veritabanıyla ana ekrandan bağlantıya basılıp gün sonu ekranı açılır (`gercek-deneme.png`).

## Handoff

- None

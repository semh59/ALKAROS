# V1-RMD-432 - PosTerminal için lint ve erişilebilirlik denetimi

- Task ID: V1-RMD-432
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

PosTerminal istemcisine, bugünkü `tsc --noEmit` tip denetimine ek olarak ESLint ve `eslint-plugin-jsx-a11y` erişilebilirlik kuralları eklenir. Böylece erişilebilirlik ve hatalı React kullanımı derleme öncesinde yakalanır. Bu iki geliştirme bağımlılığı Semih'in 2026-09-28 onayıyla eklenir.

## Owned surface

- `src/Clients/PosTerminal/eslint.config.js`
- `plan/v1/remediation/V1-RMD-432-posterminal-lint-accessibility.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/package.json src/Clients/PosTerminal/pnpm-lock.yaml
- `src/Clients/PosTerminal/.oxlintrc.json` (yeni) — Semih'in 2026-09-29 kararı: PosTerminal TypeScript 7 kullandığı
  ve ESLint'in TSX ayrıştırıcısı (typescript-eslint) TypeScript 6.1 altını desteklediği için ESLint ve
  eslint-plugin-jsx-a11y yerine jsx-a11y ve React kuralları yerleşik tek geliştirme bağımlılığı Oxlint kullanılır;
  yapılandırma bu dosyadadır
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/pnpm-workspace.yaml — yalnız Oxlint sürümünün
  yayın yaşı ayarı gerekirse

## In scope

1. `lint` betiği eklenir ve `build` betiği bu betiği de çalıştırır.
2. Bugün var olan ihlaller kural kapatılarak değil, dosya bazlı ve gerekçeli bir geçiş listesiyle kayıt altına alınır; liste yalnız küçülebilir.

## Out of scope

- WaiterPwa ve Cashier vanilla JavaScript istemcileri.
- Mevcut ihlallerin düzeltilmesi.

## Dependencies

- None

## Acceptance evidence

- `pnpm run lint`, `pnpm run typecheck` ve `pnpm test` exit code 0 verir.
- Mutasyon kontrolü: etiketsiz bir düğme eklenince lint kırmızı olur, geri alınınca yeşile döner; çıktı `evidence/V1-RMD-432/` altına kaydedilir.
- Semih için senaryo: ekran okuyucu ile kasa ekranındaki düğmelerin adlarının okunduğu görülür.

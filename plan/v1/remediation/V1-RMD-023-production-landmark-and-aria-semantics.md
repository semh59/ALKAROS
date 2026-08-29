# V1-RMD-023 - Production landmark and ARIA semantics

- Task ID: V1-RMD-023
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Goal

Production shell ve masa çalışma alanındaki accessible-name kullanımını geçerli semantic role'lara bağlamak; nested
complementary landmark'ı kaldırarak canlı production DOM taramasındaki serious/moderate bulguları kapatmak.

## Owned surface

- PO:2026-08-29 kararıyla ProductionShell.tsx yüzeyi V1-RMD-042'ye devredildi; bu historical task closed kalır.
- `src/Clients/PosTerminal/src/shell/ProductionShell.test.tsx`
- `evidence/V1-RMD-023/**`
- PO:2026-08-28 desktop floor kararıyla TableWorkspace source/test yüzeyi V1-RMD-028'e devredildi; shell ownership'i
  değişmedi ve bu historical task closed kalır.

## Dependencies

- V1-RMD-016
- V1-RMD-022

## Acceptance evidence

- Visible ALKAROS brand text gereksiz/prohibited ARIA label taşımaz; identity, filter ve stats grupları geçerli semantic
  role ile adlandırılır.
- Table details yüzeyi başka bir landmark içine nested complementary landmark üretmez ve kendi accessible name'ini
  korur.
- Component axe testleri `aria-prohibited-attr` ve `landmark-complementary-is-top-level` bulgularını sıfıra indirir;
  canlı production DOM axe-core taramasında critical/serious violation veya serious incomplete kayıt kalmaz.
- İlgili component testleri, `pnpm test`, `pnpm run typecheck`, `pnpm run build` ve `git diff --check` exit code `0`
  verir.
- Semih keyboard-only akışta shell kimliğini, masa filtrelerini, özetini ve seçili masa bağlamını screen reader
  landmarks listesinde tekil ve anlaşılır adlarla doğrular.

## Handoff

- V1-RMD-010

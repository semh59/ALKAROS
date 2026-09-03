# V1-RMD-099 - Design system token unification (handoff v1)

- Task ID: V1-RMD-099
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`design_handoff_alkaros_v1` incelemesinin doğrulanan tek somut çıktısını uygulamak:
PosTerminal design-system token setini WebPrototype/DESIGN.md ile ortak tabana
çekmek (Manrope öne, `--ds-font-mono`, coursing/heat/target/undo token grupları) ve
`printer-routing-precedence.md` Kural 2'nin "devre dışı rota bir alt spesifikliğe
düşer, Default'a sıçramaz" davranışını izole bir birim testle sabitlemek.

## Owned surface

- `plan/v1/remediation/V1-RMD-099-design-system-token-unification.md`
- `src/Clients/PosTerminal/src/design-system/tokens.css`
- `src/Clients/PosTerminal/index.html`
- `tests/Modules/Kitchen/Routing/RoutingUnitTests.cs`
- `.design-sync/**`
- `evidence/V1-RMD-099/**`
- Yüzey devri: `src/Clients/PosTerminal/src/design-system/tokens.css` custody'si
  `V1-RMD-016`'dan bu göreve geçer; `V1-RMD-016` historical `Done` kalır.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- `tokens.css`: `--ds-font-sans` Manrope ile başlar (Inter yalnız fallback adı).
  Yeni tokenler: `--ds-font-mono`, `--ds-color-dessert`/`-soft`,
  `--ds-color-disabled-ink`/`-track`, `--ds-course-1..3`(+`-soft`),
  `--ds-heat-fresh/active/stale` + `--ds-heat-*-max-minutes`,
  `--ds-target-primary` (48px), `--ds-target-touch` (52px), `--ds-undo-window` (60s).
  Mevcut 20 renk + 6 boşluk + 2 yarıçap + shell ölçüleri byte-byte korunur;
  `primitives.css`/`primitives.tsx` hiçbir yeni değişkeni zorunlu kılmaz.
- `index.html`: Manrope + DM Mono için `fonts.googleapis.com` preconnect + stylesheet
  link'i. Çevrimdışı terminal notu yorum olarak kalır (self-host + `@font-face`;
  token değerleri değişmez).
- `RoutingUnitTests.cs`: `Rule2DisabledItemPrinterFallsToNextSpecificityNotDefault` —
  Item yazıcısı devre dışı + aktif Product rotası varken sonucun `RouteLevel.Product`
  olduğunu (Default değil) doğrular.
- `.design-sync/`: `build-ds-dist.mjs` Manrope + DM Mono `@font-face` üretir
  (Fontsource 5.3.0); `conventions.md` yeni token gruplarını dokümante eder; Claude
  Design projesi (`11e13b39`) `resync.mjs` ile yenilenir.

## Out of scope

- WaiterPwa / Cashier vanilla istemcilerinin `--font-sans` yığınını değiştirmek
  (ayrı "kabuk birleştirme" işi).
- `design_handoff_alkaros_v1` incelemesinin diğer maddeleri: A (rezervasyon rol
  kapsamı) `V1-RMD-100`'e aittir; B/D/E kod tarafı zaten `Done` (`V1-ORD-003`,
  `V1-KIT-001`, `V1-KIT-002`); F `V0-PRN-001` (Blocked) beklemededir.
- Yeni bileşen, ekran veya API davranışı üretmek.

## Dependencies

- V1-RMD-016
- V1-RMD-068

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release --no-restore` 0 uyarı / 0 hata; ilgili
  testler exit 0: `RoutingUnitTests` 17/17 (yeni test dahil),
  `pnpm --dir src/Clients/PosTerminal typecheck && pnpm ... test && pnpm ... build`
  exit 0 (frontend 102 test).
- Migration yok.
- Semih PosTerminal'i açar (`docker compose -f compose.yaml -f compose.dev.yaml
  up -d`); herhangi bir ekranda gövde yazı tipinin Manrope'a döndüğünü ve
  düzenin bozulmadığını gözle doğrular. Claude Design projesinde
  (`claude.ai/design/p/11e13b39-01da-4574-94ab-992beb343152`) kartların Manrope
  ile render olduğunu görür.

## Handoff

- V1-RMD-100

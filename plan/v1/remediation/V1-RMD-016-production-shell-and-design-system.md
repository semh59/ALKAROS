# V1-RMD-016 - Production shell and design system

- Task ID: V1-RMD-016
- Status: Done
- Assignee: /root/rmd016_prod_shell
- Work type: implementation
- Surface state: Planned

## Goal

PosTerminal için role-aware navigation, persistent system status ve responsive workspace frame içeren production shell
ile semantic design-system primitives üretmek. Shell hiçbir mock runtime, fabricated data veya hard-coded success yolu
içermez.

## Owned surface

- `src/Clients/PosTerminal/src/shell/index.ts`
- `src/Clients/PosTerminal/src/shell/models.ts`
- `src/Clients/PosTerminal/src/design-system/**`
- `evidence/V1-RMD-016/**`

## Dependencies

- V1-GOV-010

## Acceptance evidence

- Role-aware navigation; branch/terminal/user identity; global session, connectivity ve freshness status; layout,
  semantic token, dialog, drawer, form ve responsive workspace primitives otomatik component/accessibility testleriyle
  geçer. Gizlenen route authorization kanıtı sayılmaz; shell `401`, `403`, offline ve stale durumlarını açık ayırır.
- 1280+ kalıcı rail ve context drawer, 768-1279 compact rail/sheet, 320-767 bottom navigation ve full-screen drill-down
  düzenleri yatay overflow olmadan çalışır. Functional colors text/icon ile desteklenir, her interactive target en az
  44x44 px, focus görünür ve reduced-motion desteklidir.
- `pnpm test`, `pnpm typecheck`, `pnpm build` ve ilgili shell/design-system testleri exit code `0` verir.
- Semih 1280x800, 768x1024 ve 390x844 üzerinde keyboard ve touch ile shell'i kullanır; rol değişince yalnız izinli
  route'ları görür, focus sırası deterministik kalır ve offline/freshness göstergesi kaybolmaz.

## Handoff

- V1-RMD-017
- V1-RMD-018
- V1-RMD-019

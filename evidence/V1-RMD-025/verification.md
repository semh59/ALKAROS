# V1-RMD-025 verification

Date: 2026-08-28
Repository root: `D:/PROJECT/ALKAROS`
Candidate commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`

## Runtime

- HTTPS Host: `https://localhost:58299`, `/health/ready` returned HTTP `200` with `{"status":"Ready"}`.
- PostgreSQL: container `alkaros-rmd025-zoom-pg-20260827`, running and accepting connections.
- Image: `postgres:18-alpine@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2`.
- Applied schema presence was verified across `audit`, `billing`, `catalog`, `customer_display`, `identity`, `kitchen`,
  `observability`, `operations`, `orders`, `public`, `reconciliation`, `reporting`, `settings`, and `table_mgmt`.
- The evidence-scoped bundle was served by the real Host. `src/Clients/PosTerminal/dist/**` remained unchanged.

## Automated browser verification

Chrome was controlled directly; no user interaction was used. Browser-level zoom shortcuts are not exposed by the
controlled Chrome surface, so the reflow behavior was exercised with equivalent CSS viewport areas derived from the
default 1280 px viewport:

- 200% equivalent: `640x248` CSS px.
- 400% equivalent: `320x124` CSS px.
- Normal mobile: `320x568` CSS px.
- Breakpoint boundaries: `767/768` and `1279/1280` CSS px.

For every matrix entry:

- horizontal overflow: `false`;
- visible interactive targets below 44x44 CSS px: `0`;
- horizontally unreachable interactive controls: `0`.

At the 400% equivalent, header, navigation, system status, and workspace return to normal document flow. The only
remaining fixed element is the off-canvas skip link; it becomes visible only when keyboard-focused. Header identity is
intentionally removed at 360 CSS px and below, leaving the ALKAROS brand, customer-display action, and logout action.
At 319 CSS px and below, the brand is also removed so the two critical actions retain their full target area.

The 26-step keyboard transcript starts with the skip link, then customer display, logout, primary navigation, page
actions, filters, view controls, table selection, and table actions. All captured focus targets are at least 44 CSS px
high. Chrome console warnings/errors: `0`.

The temporary viewport override was reset. Final browser metrics: `1280x495`, `devicePixelRatio=3`,
`visualViewport.scale=1`.

## Commands

- `pnpm --dir src/Clients/PosTerminal test -- --run` -> exit `0`; 10 files, 65 tests passed.
- `pnpm --dir src/Clients/PosTerminal typecheck` -> exit `0`.
- `pnpm --dir src/Clients/PosTerminal exec vite build --outDir D:/PROJECT/ALKAROS/evidence/V1-RMD-025/browser-dist --emptyOutDir`
  -> exit `0`; 67 modules transformed.

## Key SHA-256 values

- `shell.css`: `93a0a1a9a3053e7e03aeec0aa36f359b00e223287031f3260a086580131bd847`
- `layout-contract.test.ts`: `60edc5eecedc3b99ff47a3859afaa6ac560deb064baa0fc1ed508dcb440da105`
- 200% screenshot: `e8b8c1dc6ffe1566e687d41f594e8aede4c167b94deb34440af0984eeedc7f68`
- 400% screenshot: `cad7461a065656083fcbc0292c7c40586b49690fdb48695aafada276958b854f`
- Browser bundle CSS: `2e4304c0187fa8164b74a43a38be1a5e76cdeb13defa7e3eacd381c823f6f99b`
- Browser bundle JS: `1581c29eb4cc4426472d05795cf202c67a4a3827a054eadca6306e7c723ad651`

## Product-quality boundary

This task closes shell reflow only. The current `Harita` mode remains a responsive card grid rather than a spatial
restaurant floor plan. It does not satisfy the broader product request for chair-aware placement, visible transfer,
merge/unmerge, reservation context, or split-bill workflows. Those gaps must remain explicit inputs to the next
desktop-first table workspace and Docker release task; this verification does not classify the product as production
ready.

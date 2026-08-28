# V1-RMD-018 validation

- Candidate commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`.
- Repository root: `D:\PROJECT\ALKAROS`; active task `V1-RMD-018`; assignee `/root`.
- Dependencies `V1-RMD-014` and `V1-RMD-016`: `Done`.
- Owned surface is limited to `src/Clients/PosTerminal/src/features/catalog/**` and this evidence directory.
- Component and client tests: `pnpm --dir src/Clients/PosTerminal test` exit `0`; 8 test files and 50 tests passed.
- Typecheck: `pnpm --dir src/Clients/PosTerminal typecheck` exit `0`.
- Production build: `pnpm --dir src/Clients/PosTerminal build` exit `0`; Vite `8.2.2` generated the bundle.
- Coverage includes searchable category/tax/product/modifier/price tabs, dense list/detail layout, manager create
  editors, exact field validation, active/effective-price authority messaging, bounded loading/busy/empty/error/
  offline/stale/unauthorized/conflict states, preserved conflict form values, named modal behavior, responsive 44px
  controls, and axe critical/serious checks.
- Real client contract tests cover all six manager reads with `limit=100`, typed product create, same-origin
  credentials, request correlation IDs, eight-second timeout, and server `409 DUPLICATE_SKU` envelope preservation.
  No publication or fabricated cashier-success behavior is included.
- Manual integrated scenario: manager creates a category, tax profile, priced product and modifier; the product is
  cashier-visible only after active/effective pricing. Invalid price and duplicate/conflicting submissions retain the
  editor and identify the exact field. Composition and browser execution are owned by `V1-RMD-020`.
- File SHA-256: `CatalogWorkspace.tsx=2FBA3065EFC30AC4A25E863DF442FCD59D53D34834DD4AE5CDDC52448978BE71`,
  `CatalogWorkspace.test.tsx=5561062D8B3C82E2437DE996F7673FE2A0BF1BBB8B5ACA28133940A19DFE1947`,
  `catalogApi.ts=E8F2796080621747F6E6103EF49156E31FC03FB91A19F318C530B802277D5E48`,
  `catalogApi.test.ts=73544F828EB75CCED00106C91E932F6FEC7A064B43255BA869BC3D4332E1A011`,
  `models.ts=A8030FD7ECB06DF966F531F1F7D438735000F8CF342E717C12674356A2E314C3`,
  `catalog.css=EAD3730DEEC52EE2A3BDC409242FCD94F9A0B1B2B75A2876EB1FE5C9B3405071`,
  `index.ts=502E520C9C633C370F90105A07C71BD14CFF6D44CB1689B1CD934F4E2783BAC9`.
- `verify-manifest` remains a governance reseal responsibility: it currently reports 19 stale rows because this task
  added new task/evidence Markdown files while `plan/AUDIT_MANIFEST.json` is outside this owned surface.

# V1-RMD-018 - Menu and catalog workspace UI

- Task ID: V1-RMD-018
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Planned

## Goal

Category, tax, product, modifier ve effective price için searchable list-detail-editor production workspace'i üretmek
ve gerçek V1-RMD-014 contract'ına bağlamak. V1 ekranı V11 menu publication capability'si varmış gibi davranmaz.

## Owned surface

- `evidence/V1-RMD-018/**`
- PO:2026-08-28 desktop menu kalite kararıyla catalog feature source/test yüzeyi V1-RMD-030'a devredildi; bu
  historical task closed kalır.

## Dependencies

- V1-RMD-014
- V1-RMD-016

## Acceptance evidence

- Searchable/filterable dense entity list, authoritative detail ve accessible create/edit drawer; category, tax profile,
  product identity/type/stock mode/SKU, modifier assignment ve effective price akışlarını gerçek API ile tamamlar.
- Default, loading, empty, busy, success, error, offline, stale, unauthorized ve conflict durumları ayrı ve bounded'dır.
  Validation summary exact field'i gösterir; failure/conflict formu korur; başarılı commit olmadan sellable catalog değişmiş
  gibi gösterilmez.
- `pnpm test`, `pnpm typecheck`, `pnpm build` ve ilgili catalog workspace testleri exit code `0` verir; keyboard, named
  dialogs, focus restoration, 44x44 target, WCAG 2.2 AA ve responsive checks geçer.
- Semih manager olarak category, tax, priced product ve modifier oluşturur; ürün yalnız active/effectively-priced iken
  cashier kataloğunda görünür. Invalid price ve deliberate conflict'te form korunur ve exact field açıklanır.

## Handoff

- V1-RMD-020

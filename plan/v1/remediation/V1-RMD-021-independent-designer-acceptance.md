# V1-RMD-021 - Independent designer acceptance

- Task ID: V1-RMD-021
- Status: Done
- Assignee: 2814a35e-cc66-4d6c-882c-1b1809271ab4
- Work type: validation
- Surface state: Existing

## Goal

V1-RMD-013..020 uygulamalarından hiçbirini yapmamış taze bir reviewer ile birleşik production deneyimini designer,
accessibility ve gerçek workflow kalitesi açısından bağımsız kabul veya red kararına bağlamak.

## Owned surface

- `docs/audit/V1_PRODUCTION_EXPERIENCE_DESIGN_ACCEPTANCE_2026-08-25.md`
- `evidence/V1-RMD-021/**`

## Dependencies

- V1-RMD-011

## Acceptance evidence

- Cashier, supervisor, manager, kitchen ve customer-display rolleri için navigation, table-to-kitchen, add-table,
  add-product, Unknown recovery ve dual-screen akışları gerçek Host/PostgreSQL üzerinde; her required state,
  viewport/breakpoint, focus order, accessibility tree, overflow/bounds, console/network ve visual hierarchy kanıtıyla
  bağımsız raporda kapsanır.
- 1920x1080, 1440x900, 1366x768, 1280x800, 1024x768, 768x1024, 430x932, 390x844 ve 320x568 ile breakpoint ±1 px;
  keyboard-only, 200/400% zoom, reduced-motion, WCAG 2.2 AA, 44x44 targets ve sıfır critical/serious automated
  accessibility finding geçer. Otomatik scan tek başına designer kabulü değildir.
- Herhangi bir missing required state, mock-backed production flow, broken table-to-kitchen/catalog workflow,
  critical/serious accessibility issue, data/permission leakage veya açık Semih kabulünün yokluğu görevi fail-closed
  bırakır.
- Semih gizli setup olmadan table creation, product creation ve table-to-kitchen service'i tamamlar; deneyimi açıkça
  kabul veya reddeder. Sessizlik approval sayılmaz.

## Handoff

- V1-GOV-004

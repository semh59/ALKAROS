# V1-RMD-017 - Table workspace UI

- Task ID: V1-RMD-017
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Planned

## Goal

Zone-filtered table map/list, table setup ve permissioned operational actions içeren production table workspace'ini
gerçek V1-RMD-013 contract'ına bağlamak. Masa ve order context'i conflict veya recovery sırasında kaybolmaz.

## Owned surface

- `evidence/V1-RMD-017/**`
- PO:2026-08-28 reddedilen card-grid yerine desktop floor kararıyla tables feature source/test yüzeyi V1-RMD-028'e
  devredildi; bu historical task closed kalır.

## Dependencies

- V1-RMD-013
- V1-RMD-016

## Acceptance evidence

- Zone/status filters; table number, capacity, textual status, elapsed time ve order/bill pointer gösteren dense
  map/list; selected-table context; manager add-zone/add-table; cashier transition/reservation/transfer/merge actions
  gerçek API ile ve capability sonucuna göre çalışır.
- Default, loading, empty, busy, success, error, offline, stale, unauthorized ve conflict durumları ayrı ve bounded'dır.
  Form/draft verisi validation veya conflict sırasında korunur; server row version ve allowed commands authoritative'dir.
- `pnpm test`, `pnpm typecheck`, `pnpm build` ve ilgili table workspace testleri exit code `0` verir; keyboard, named
  dialogs, focus restoration, 44x44 target, WCAG 2.2 AA ve responsive checks geçer.
- Semih manager olarak `Salon` zone'u ve `S-09` masasını ekler; cashier olarak masayı açar, deliberate stale row-version
  conflict üretir ve mevcut order context'ini kaybetmeden authoritative state'e döner.

## Handoff

- V1-RMD-020

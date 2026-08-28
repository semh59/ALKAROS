# V1-RMD-020 - Production experience composition and real E2E

- Task ID: V1-RMD-020
- Status: Done
- Assignee: /root
- Work type: integration
- Surface state: Existing

## Goal

Table, catalog ve kitchen/operations API gruplarını Host'a map etmek; production shell ile workspace'leri mevcut cashier
ve customer-display akışlarına compose etmek; birleşik V1 deneyimini gerçek HTTPS Host ve PostgreSQL üzerinde
mock-success olmadan doğrulamak.

## Owned surface

- `src/Clients/PosTerminal/.gitignore`
- `src/Clients/PosTerminal/index.html`
- `src/Clients/PosTerminal/pnpm-workspace.yaml`
- `src/Clients/PosTerminal/src/stale.ts`
- `src/Clients/PosTerminal/src/stale.test.ts`
- `src/Clients/PosTerminal/tsconfig.json`
- `tests/Host/Experience/Composition/**`
- `evidence/V1-RMD-020/**`
- PO:2026-08-28 complete container release kararıyla Host/PosTerminal composition, package/build ve experience test
  yüzeyi V1-RMD-031'e devredildi; bu historical task closed kalır.

## Dependencies

- V1-RMD-017
- V1-RMD-018
- V1-RMD-019
- V1-RMD-009

## Acceptance evidence

- Üç Experience API group'u Host composition'a map edilir; shell/workspace routing mevcut login, terminal-bound cashier,
  order mutation, customer-display allowlist DTO, pairing/revoke ve ayrı storage/cookie sınırını korur. Manager, cashier,
  kitchen ve customer deep-link/permission denials gerçek HTTP testinde `401/403` ayrımıyla geçer.
- Locked Release build ile tüm ilgili .NET/Node/TypeScript testleri exit code `0` verir. Digest-pinned temporary PostgreSQL
  ve trusted disposable HTTPS sertifika zinciriyle browser E2E çalışır; mock runtime, fabricated fixture response veya
  hard-coded success production bundle'a giremez.
- E2E login, add table, add product, table order, submit, kitchen transition, reconnect, Host restart, stale, conflict,
  revoke ve customer-display data minimization akışlarını iki bağımsız browser storage alanında kanıtlar.
- Semih gerçek browser'da login olur, masa ve priced product ekler, masaya sipariş açıp mutfağa gönderir, kitchen state'i
  ilerletir; reconnect/restart sonrasında cashier/customer state'lerini ve yetkisiz rol reddini gözlemler.

## Handoff

- V1-RMD-010

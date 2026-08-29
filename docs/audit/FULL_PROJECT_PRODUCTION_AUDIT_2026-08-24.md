# ALKAROS Full Project Production Audit — 2026-08-24

## Verdict

NOT PRODUCTION READY

Pinned candidate `a03d02146961c29a8b847a7b0c472c6c8dd42c9f` / tree
`39e9bb79d3d6f3e099e15a7ffafcc801f727843a` contains 1,727 tracked paths. The ledger covers every path and every UTF-8
text line range; binary assets are hash/signature inventoried. This is not a blanket PASS: historical evidence is
explicitly not reused, and uncompleted dynamic tests remain blockers.

## Finding distribution

- P0 blocker: 1
- P1 high: 14
- P2 medium: 10
- P3 low: 0

## Independent results

- Clean disposable checkout: candidate/tree/path count matched before tests.
- PostgreSQL 18 digest-pinned forward `001..038`: exit 0. Reverse failed at `012` because `btree_gist` still backs
  `catalog.product_prices.excl_product_prices_no_overlap`; container was removed.
- PosTerminal frozen install, typecheck, two unit tests and production build: exit 0.
- WebPrototype Node tests: 20/20 pass; tooling Node tests: 7/7 pass. These are mock/unit evidence, not production E2E.
- Plan validate: exit 1. PDF coverage: exit 0. Audit manifest verification: exit 1 with 1,601 errors.
- Required .NET 10.0.302 and host `psql` were unavailable. Bundled Python is 3.12.13 rather than locked 3.12.12 and
  lacks pytest. No result from these missing tools is treated as PASS.
- Browser evidence covers the unauthenticated production login at nine viewports plus CSS breakpoints ±1, and the
  initial display failure. Authenticated real-HTTPS states are blocked and routed to final verification.
- WebPrototype rendered at desktop/mobile widths; mobile hides explicit MOCK identity and multiple targets are <44px.

## Confirmed findings

| ID | Severity | Title | Owner |
| --- | --- | --- | --- |
| AUD-001 | P1 high | Audit manifest and V1 closure claims are stale | V1-RMD-007 |
| AUD-002 | P1 high | Assembly provenance is pinned to an older commit | V1-RMD-007 |
| AUD-003 | P1 high | CI does not enforce clean restore/build/test/security/coverage | V1-RMD-007 |
| AUD-004 | P1 high | Cashier mutations authenticate but never authorize permissions | V1-RMD-008 |
| AUD-005 | P1 high | Rate limits are global named windows rather than caller partitions | V1-RMD-008 |
| AUD-006 | P1 high | Plain HTTP and proxy/TLS boundaries are not fail-closed | V1-RMD-008 |
| AUD-007 | P1 high | Unhandled 500 failures are not logged | V1-RMD-008 |
| AUD-008 | P2 medium | Raw exception text is persisted into operational records | V1-RMD-008 |
| AUD-009 | P1 high | Production HTTP surface has no end-to-end integration tests | V1-RMD-008 |
| AUD-010 | P1 high | PostgreSQL 001..038 rollback chain fails at migration 012 | V1-RMD-009 |
| AUD-011 | P1 high | Cumulative order quantity bypasses the 999 limit | V1-RMD-009 |
| AUD-012 | P2 medium | Catalog endpoint is unbounded and has no pagination contract | V1-RMD-009 |
| AUD-013 | P2 medium | Customer display contract states collapse to generic active UI | V1-RMD-010 |
| AUD-014 | P1 high | First non-401 display snapshot failure leaves infinite loading | V1-RMD-010 |
| AUD-015 | P2 medium | Pairing completion failures are hidden | V1-RMD-010 |
| AUD-016 | P2 medium | Authenticated POS controls violate 44x44 touch targets | V1-RMD-010 |
| AUD-017 | P2 medium | Focus indicator contrast is below the non-text threshold | V1-RMD-010 |
| AUD-018 | P2 medium | Authenticated UI/accessibility state matrix is unverified | V1-RMD-010 |
| AUD-019 | P2 medium | WebPrototype loses its explicit MOCK identity on mobile | V1-RMD-011 |
| AUD-020 | P2 medium | WebPrototype has no CSP shipping boundary | V1-RMD-011 |
| AUD-021 | P2 medium | WebPrototype touch targets are below 44x44 | V1-RMD-011 |
| AUD-022 | P1 high | Required .NET/psql/Python validation toolchain is unavailable | V1-GOV-004 |
| AUD-023 | P1 high | Security/supply-chain closure is incomplete | V1-RMD-007 |
| AUD-024 | P1 high | Database concurrency/restart/timeout/performance acceptance is incomplete | V1-RMD-009 |
| AUD-025 | P0 blocker | External go-live evidence and signed approval are absent | V1-GOV-004 |

## Release blockers

The candidate cannot be approved until all repository findings are remediated and independently rerun with the exact
toolchain. Fiscal device, printer, QNB, Yemeksepeti, meal-card, QR relay, licensing, backup/RPO-RTO, independent
security assessment and dated signed go-live evidence are absent; no waiver was created.

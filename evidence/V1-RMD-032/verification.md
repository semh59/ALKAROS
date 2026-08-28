# V1-RMD-032 verification

## Custody

- Repository root: `D:\PROJECT\ALKAROS`
- Task: `V1-RMD-032`
- Assignee: `/root`
- Base commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Base tree: `39e9bb79d3d6f3e099e15a7ffafcc801f727843a`
- Writable surface: the acceptance report, this task evidence, and task status/assignee metadata only.
- The worktree contained extensive pre-existing remediation changes. No production source was edited by this
  validation task.

## Commands and exit codes

| Command | Exit | Result |
| --- | ---: | --- |
| `docker build --target ui-build --tag alkaros-ui-audit:20260828 .` | 0 | Frozen pnpm install and `pnpm build` completed; image manifest list `sha256:99fbcf6ba4165ddac66f9d0006c24a08f1323565eaa5f236b186341fdb88059a`. |
| `docker run --rm alkaros-ui-audit:20260828 pnpm test` | 0 | 13 test files, 77 tests passed. |
| `docker compose restart postgres host proxy` | 0 | Services restarted. The immediately chained readiness request ran before health recovery and failed as expected; the bounded follow-up is authoritative. |
| Bounded HTTPS readiness loop | 0 | `https://localhost:8443/health/ready` returned 200; postgres, host and proxy became healthy in 13 seconds. |
| PostgreSQL persistence counts | 0 | zones=1, tables=3, products=1, orders=1, tickets=1 after restart. |
| Placeholder/static scan | 0 | No TODO/FIXME/NotImplemented/mock-success pattern in the scoped production surfaces. |

## Runtime facts

- Queued kitchen ticket: `150f10ec-6064-4022-ba42-ec96abf32741`, order
  `7b6c9e74-83e5-43c5-b490-f40879b34668`, station `kitchen-main`, status `Queued`.
- Reservation: party size persisted as 1 although reason text states four people; expiry is null.
- Floor plans: 0.
- Table merges: 0 after two attempted workflows.
- Bills: 0 after the submitted order.
- Browser console warnings/errors in exercised final routes: 0.
- Invalid pairing error surfaced in 588 ms.
- Display revoke cleared the order/financial snapshot in 1,864 ms (threshold 10,000 ms).

## Browser evidence limitations

- Native Ctrl+/Ctrl= zoom did not change viewport/DPR in the controlled browser backend. The supplied 200/400%
  images are CSS-pixel reflow equivalents and are labeled accordingly.
- Live axe injection is not supported by the controlled page API. Component-level axe tests passed, but this does not
  replace a live production route audit.
- Escape close and return-focus passed. A reliable complete Tab-order transcript was not obtained, so it is not
  claimed as passed.
- Separate browser tabs generated distinct display codes and proved the paired/revoked flow, but a completely separate
  browser profile/context was not available in this harness.

## Autonomous scenario

The reviewer logged in, created a zone and three tables, created and priced one catalog product, opened and submitted
an order, verified the database kitchen ticket, paired a separate customer-display tab, observed the submitted order
and total, revoked it, measured stale-data clearing, exercised reservation, transfer and merge attempts, restarted the
stack, and verified readiness plus data persistence. The user was not asked to perform browser validation.

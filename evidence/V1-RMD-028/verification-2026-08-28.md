# V1-RMD-028 verification

## Delivered behavior

- Replaced the card-like map with a desktop-first spatial floor-plan canvas and a dense accessible mobile list.
- Rendered authoritative zone dimensions, table geometry and shape, seat positions, lifecycle state, elapsed
  occupancy, order, bill, reservation and merge context.
- Added separate operation and setup modes with pointer drag, keyboard move/resize/rotate, numeric geometry editing,
  seat editing, validation review, atomic save and conflict-preserving draft behavior.
- Bound visible table actions to the server command list. Unsupported reservation mutations remain fail-closed
  because the current server DTO omits their required reservation row version.
- Added complete target-table concurrency data to transfer requests and an encoded, fully versioned unmerge client route.
- Preserved seat anchors when a table moves or resizes.

## Automated checks

| Check | Result |
| --- | --- |
| `pnpm typecheck` | Exit 0 |
| `pnpm test -- --run` | Exit 0; 11 files and 70 tests passed |
| task-owned production Vite build | Exit 0; 69 modules transformed |
| task-owned browser harness build | Exit 0; 24 modules transformed |
| `python tools/plan-audit/plan_audit_tool.py validate` | Exit 0; 0 errors and 0 warnings |
| `npx.cmd --yes markdownlint-cli2@0.23.2 ...` | Exit 0; 0 issues |
| scoped `git diff --check` | Exit 0 |

The component suite includes an axe-core gate with zero critical or serious violations. The shared modal dependency
separately tests named modal semantics, focus trapping, Escape closure and focus restoration.

## Browser acceptance

`browser-matrix.json` and `browser-transcript.md` record the autonomous browser run. All 17 requested and boundary
viewports passed without horizontal overflow, clipped critical surfaces, undersized interaction targets or console
errors. The run also proves operation actions, keyboard-only setup, atomic save, 200% and 400% equivalent reflow,
modal focus restoration and the exact 768/767 responsive transition.

## Handoff boundary

Production composition remains owned by `V1-RMD-031`; this task provides the complete tested component and API
surface without changing `App.tsx`. Server publication of reservation row versions and the `Unmerge` allowed command
is also deferred to that integration surface; the current UI fails closed instead of issuing unverifiable mutations.

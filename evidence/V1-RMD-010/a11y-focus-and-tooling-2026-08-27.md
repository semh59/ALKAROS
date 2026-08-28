# V1-RMD-010 accessibility and designer measurement record — 2026-08-27

## Measured passes

- Nine target viewport dimensions were measured against the real HTTPS Host. Every measured viewport had
  `documentElement.scrollWidth` equal to the layout client width and `overflow-x: clip`; no horizontal overflow was
  found.
- CSS media boundaries were measured at ±1px for `1250`, `900`, `767`, `430`, table `1279`, and kitchen `1023`.
  All captured boundary screenshots have horizontal overflow `false` and visible target minimum `44px`.
- Visible controls in the cashier matrix were at least `46px` in the initial shell. The active-order quantity controls
  were `44×44px`; the sticky order CTA was larger.
- The production shell has named headings, landmarks, buttons/links, a named pairing dialog, `aria-modal="true"`,
  live status/alert regions, and keyboard Escape/focus restoration behavior.
- Visual inspection of the 390px cashier and 429px table screens found the critical total/status and bottom navigation
  readable without horizontal clipping. The narrow header intentionally truncates secondary role text with no layout
  overflow.

## Contrast and automated tooling disposition

- The repository includes `axe-core 4.10.3` as a locked PosTerminal dev dependency.
- The in-app browser bridge does not expose page script injection. Attempting to inject the locked axe source failed at
  the bridge boundary with `TypeError: document.createElement is not a function`. No axe result is claimed from that
  attempt.
- The bridge exposes an explicit viewport override but no browser zoom or media emulation API. Therefore real
  200%/400% zoom and `prefers-reduced-motion` acceptance cannot be truthfully marked passed in this environment.
- Focus ring contrast on both light and dark backgrounds was not independently color-sampled by the bridge. Source CSS
  and visual screenshots are retained, but this criterion remains open for an environment with computed-style/color
  sampling and axe execution.

## Fail-closed findings

1. **P1 — keyboard-accessible table quick action is not independently reachable.**
   `src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx:276-280` renders a clickable
   `span.table-card__quick-action` inside a parent `<button>`. The span has no keyboard target and creates nested
   interactive semantics. This is outside RMD-010’s evidence-only owned surface and must be remediated by the
   table-workspace owner before independent designer acceptance.
2. **P1 — required zoom/reduced-motion/automated-a11y gates are unproven in the current browser bridge.**
   This is an evidence-environment blocker, not a pass. A fresh designer-capable browser run is required before the
   production gate can close.

No source change was made by V1-RMD-010.

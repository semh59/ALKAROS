# ALKAROS device / browser test plan

> Source basis: EXT:WCAG-2.2, PO:2026-09-01
> Owner task: V1-RMD-092
> Accessibility target authority: `docs/compliance/accessibility-target.md` (V0-CMP-005)

## 1. Target matrix

From `docs/compliance/accessibility-target.md` §4, the surfaces ALKAROS V1 must
be verified on:

| Client | Device | OS | Browser | Screen reader |
| --- | --- | --- | --- | --- |
| Cashier POS (`src/Clients/Cashier`) | Touch-screen POS | Windows 11 | Chrome (kiosk mode) | N/A (touch-only kiosk) |
| Waiter PWA (`src/Clients/WaiterPwa`) | Handheld tablet | Android 14 | Chrome | TalkBack |
| PosTerminal (`src/Clients/PosTerminal`) | Back-office / manager desktop | Windows 11 / macOS | Chrome / Firefox / Edge | NVDA / VoiceOver |
| DualScreen customer display | Fixed screen | Windows 11 | Chrome (kiosk) | N/A (display-only) |

WCAG target: **2.2 Level AA** for every surface, with one approved exception
(EXC-001: Cashier POS waives 2.4.11 Focus Appearance for touch-only kiosk mode).
The level decision (V0-CMP-005-D001) and EXC-001 were approved on 2026-09-01 by
Semih (Founder / Product Owner) — see `docs/compliance/accessibility-target.md`.

### Responsive breakpoints (PosTerminal shell)

`classifyViewport(px)` → `mobile` (< 768), `compact` (768–1279), `wide` (≥ 1280).
Below the wide breakpoint the context panel collapses into an explicit sheet.

## 2. Automated coverage

Run with `pnpm --dir src/Clients/PosTerminal test` (part of the wave reseal gate).

| Area | Test | What it checks |
| --- | --- | --- |
| Shell reflow | `src/shell/ProductionShell.test.tsx` | `classifyViewport` at 320 / 767 / 768 / 1279 / 1280 / 1920 px; context becomes a sheet below `wide`; focus moves to the sheet close button; Escape closes it |
| Shell a11y | `src/shell/ProductionShell.test.tsx` | axe: no critical/serious violations |
| Design primitives a11y | `src/design-system/primitives.test.tsx` | axe on the primitive set |
| Catalog / Tables / Floor plan / Kitchen / Billing a11y | `src/features/*/**.test.tsx` | axe on each workspace |
| Stale-shell a11y | `src/stale.test.ts` | axe on the offline/stale shell |
| Vanilla client shells | `src/vanilla-clients-a11y.test.ts` | Cashier + Waiter PWA `index.html`: `lang`, `title`, zoom-permitting viewport meta, a landmark, and axe (no critical/serious) on the shipped shell markup |

**Contrast (WCAG 1.4.3 / 1.4.11):** axe's `color-contrast` rule is disabled in
these tests because jsdom cannot compute rendered colours. Contrast is instead
enforced by the design-system tokens (`src/Clients/PosTerminal/src/design-system`)
and re-checked on real devices in the manual pass below.

**Dynamic state of the vanilla clients:** the smoke test covers the static shell
only (before `cashier-app.js` / `waiter-app.js` render). Rendered-state a11y for
those clients is covered by the manual checklist and by the fact that their DOM
patterns mirror the PosTerminal workspaces that do have full axe coverage.

## 3. Manual pre-go-live checklist (physical devices)

Automation cannot replace a real device, real touch, a real screen reader, or a
real printer. Before go-live, on the actual hardware from the matrix:

### Cashier POS (Windows 11 touch-screen, Chrome kiosk)

- [ ] Kiosk-mode Chrome full-screen; no browser chrome reachable.
- [ ] Every primary action reachable with one thumb tap; targets ≥ 44 × 44 px.
- [ ] Barcode scanner input focuses the search field and submits.
- [ ] Pinch-zoom works (text can be enlarged) — WCAG 1.4.4.
- [ ] Order dispatch → kitchen ticket prints on the real printer.
- [ ] Park / recall a ticket; totals and item list survive.
- [ ] Pull the network cable: the client fails closed (no silent mock catalog).

### Waiter PWA (Android 14 tablet, Chrome, TalkBack)

- [ ] Installs to home screen from the manifest; launches full-screen.
- [ ] Pinch-zoom works (viewport no longer blocks it) — WCAG 1.4.4.
- [ ] TalkBack: header, status ribbon, zone nav, tables grid and the order
      drawer are all announced with meaningful names.
- [ ] Turn WiFi off mid-order: ribbon shows `Çevrimdışı • İşlemler Güvenli
      Kuyrukta`; turn WiFi on: the queued order reaches the server.
- [ ] Over plain `http://<lan-ip>` the app shows `Çevrimdışı mod kapalı` and does
      not pretend to queue.
- [ ] Target sizes ≥ 44 px; one-hand reachability for seat / add-item.

### PosTerminal (desktop, Chrome + Firefox + Edge, NVDA / VoiceOver)

- [ ] 200 % browser zoom: no horizontal scroll, no clipped controls — WCAG 1.4.10.
- [ ] 320 px width: shell is usable, context is a sheet.
- [ ] NVDA / VoiceOver: landmark navigation reaches every workspace; forms
      announce labels and errors.
- [ ] Keyboard only: full navigation, visible focus, no traps.

### DualScreen customer display

- [ ] Renders the order and running total at the customer-facing screen size.
- [ ] Recovers after a display reconnect / host restart.

Record the run (date, tester, device build, pass/fail per item) under
`evidence/` before flipping the go-live gate.

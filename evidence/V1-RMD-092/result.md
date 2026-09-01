# V1-RMD-092 - Device browser test plan and vanilla client a11y smoke

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi

## Deliverables

1. **`docs/qa/device-browser-test-plan.md`** — consolidates:
   - the device/browser matrix (authority: `docs/compliance/accessibility-target.md`, V0-CMP-005) — Cashier POS / Waiter PWA / PosTerminal / DualScreen, WCAG 2.2 AA, EXC-001;
   - the automated-coverage map (PosTerminal `ProductionShell` breakpoint test at 320/767/768/1279/1280/1920 px + 8 workspace axe suites + the new vanilla-client smoke);
   - the manual pre-go-live physical-device checklist per client (kiosk mode, touch targets ≥ 44 px, real screen reader, real printer, WaiterPwa offline toggle on a real phone, 200 % zoom reflow);
   - the note that axe `color-contrast` is disabled under jsdom (contrast enforced by design tokens + manual pass);
   - the note that the V0-CMP-005 WCAG-level decision + EXC-001 approval are still pending Semih's sign-off.

2. **`src/Clients/PosTerminal/src/vanilla-clients-a11y.test.ts`** (new, 6 tests) —
   loads `src/Clients/Cashier/wwwroot/index.html` and
   `src/Clients/WaiterPwa/wwwroot/index.html` as shipped and asserts per client:
   - `<html lang="tr">`, non-empty `<title>`, a viewport meta that does **not**
     contain `user-scalable=no` / `maximum-scale=1`;
   - at least one landmark;
   - axe (`color-contrast` disabled) reports no `critical` / `serious` violation.

3. **`src/Clients/WaiterPwa/wwwroot/index.html`** — viewport meta fix.

## Defect found and fixed

The smoke initially failed on **Waiter PWA**:

```
meta-viewport (critical):
  <meta name="viewport" content="width=device-width, initial-scale=1.0,
        maximum-scale=1.0, user-scalable=no, viewport-fit=cover">
```

`user-scalable=no` + `maximum-scale=1.0` block pinch-zoom, failing **WCAG 2.2
1.4.4 Resize Text (AA)** — a real problem for a low-vision waiter on a tablet.

Fixed to:

```
<meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">
```

`viewport-fit=cover` kept for the iOS notch. Cashier POS: 0 critical/serious, no
change needed.

## Result

```
pnpm exec vitest run src/vanilla-clients-a11y.test.ts
Test Files  1 passed (1)
     Tests  6 passed (6)
```

Full `pnpm --dir src/Clients/PosTerminal test`: 96 tests pass (90 + 6).

## Scope note

Physical-device testing (real tablets, real touch, real screen readers, real
printers) stays a **manual** go-live gate — the checklist in the plan doc is the
record. Playwright / real-browser automation is out of scope for V1.

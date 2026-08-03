# Accessibility Conformance Target

> **Task:** V0-CMP-005
> **Status:** Blocked
> **Assignee:** codex-v0-cmp-005
> **Work type:** decision
> **Source basis:** EXT:WCAG-2.2, CORR:C19
> **Date:** 2026-07-30

## 1. Decision Record

| Field | Value |
| ------- | ------- |
| **Decision ID** | V0-CMP-005-D001 |
| **Date** | 2026-07-30 |
| **Approver** | TBD (onay bekliyor) |
| **Selected result** | WCAG 2.2 Level AA for all UI surfaces |
| **Rejected alternatives** | Level A only (insufficient for public-facing restaurant POS); Level AAA (impractical for cashier speed-critical UI) |

## 2. UI Surface Inventory

| Surface ID | Description | Target Level | Applicable Criteria | Exception |
| ------------ | ------------- | -------------- | --------------------- | ----------- |
| CUI | Cashier UI (touch-screen POS) | WCAG 2.2 AA | All AA criteria except 2.4.11 (Focus Appearance) — waived for touch-only kiosk mode | 2.4.11: Touch-only device, no keyboard focus indicator needed |
| WTR | Waiter UI (handheld tablet) | WCAG 2.2 AA | All AA criteria | None |
| PUI | Customer QR UI (mobile browser) | WCAG 2.2 AA | All AA criteria | None |
| OUI | Operations UI (back-office web) | WCAG 2.2 AA | All AA criteria | None |
| CWB | Customer-facing web (menu/ordering) | WCAG 2.2 AA | All AA criteria | None |

## 3. Selected Success Criteria (AA Level)

### Perceivable

- 1.1.1 Non-text Content (A)
- 1.2.1 Audio-only and Video-only (A) — N/A for POS
- 1.2.2 Captions (A) — N/A
- 1.2.3 Audio Description or Media Alternative (A) — N/A
- 1.2.4 Captions (Live) (AA) — N/A
- 1.2.5 Audio Description (Prerecorded) (AA) — N/A
- 1.3.1 Info and Relationships (A)
- 1.3.2 Meaningful Sequence (A)
- 1.3.3 Sensory Characteristics (A)
- 1.3.4 Orientation (AA)
- 1.3.5 Identify Input Purpose (AA)
- 1.4.1 Use of Color (A)
- 1.4.2 Audio Control (A)
- 1.4.3 Contrast (Minimum) (AA)
- 1.4.4 Resize Text (AA)
- 1.4.5 Images of Text (AA)
- 1.4.10 Reflow (AA)
- 1.4.11 Non-text Contrast (AA)
- 1.4.12 Text Spacing (AA)
- 1.4.13 Content on Hover or Focus (AA)

### Operable

- 2.1.1 Keyboard (A)
- 2.1.2 No Keyboard Trap (A)
- 2.1.4 Character Key Shortcuts (A)
- 2.2.1 Timing Adjustable (A)
- 2.2.2 Pause, Stop, Hide (A)
- 2.3.1 Three Flashes or Below Threshold (A)
- 2.4.1 Bypass Blocks (A)
- 2.4.2 Page Titled (A)
- 2.4.3 Focus Order (A)
- 2.4.4 Link Purpose (In Context) (A)
- 2.4.5 Multiple Ways (AA)
- 2.4.6 Headings and Labels (AA)
- 2.4.7 Focus Visible (AA)
- 2.4.11 Focus Appearance (AA) — **Exception: CUI (touch-only)**
- 2.4.12 Focus Not Obscured (AA)
- 2.5.1 Pointer Gestures (A)
- 2.5.2 Pointer Cancellation (A)
- 2.5.3 Label in Name (A)
- 2.5.4 Motion Actuation (A)
- 2.5.7 Dragging Movements (AA)
- 2.5.8 Target Size (AA)

### Understandable

- 3.1.1 Language of Page (A)
- 3.1.2 Language of Parts (AA)
- 3.2.1 On Focus (A)
- 3.2.2 On Input (A)
- 3.2.3 Consistent Navigation (AA)
- 3.2.4 Consistent Identification (AA)
- 3.3.1 Error Identification (A)
- 3.3.2 Labels or Instructions (A)
- 3.3.3 Error Suggestion (AA)
- 3.3.4 Error Prevention (Legal, Financial, Data) (AA)

### Robust

- 4.1.2 Name, Role, Value (A)
- 4.1.3 Status Messages (AA)

## 4. Test Device/Browser Matrix

| Device | OS | Browser | Screen Reader |
| -------- | ---- | --------- | --------------- |
| Cashier touch-screen | Windows 11 | Chrome (kiosk mode) | N/A (touch-only) |
| Waiter tablet | Android 14 | Chrome | TalkBack |
| Customer phone | iOS 18 / Android 14 | Safari / Chrome | VoiceOver / TalkBack |
| Operations desktop | Windows 11 / macOS | Chrome / Firefox / Edge | NVDA / VoiceOver |

## 5. Exception Register

| Exception ID | Surface | Criterion | Rationale | Approval Date | Approver |
| ------------- | --------- | ----------- | ----------- | --------------- | ---------- |
| EXC-001 | CUI | 2.4.11 Focus Appearance | Touch-only kiosk mode; no keyboard focus indicator needed | TBD (onay bekliyor) | TBD (onay bekliyor) |

## 6. Affected Tasks

- V1-CUI-001, V1-CUI-002, V1-CUI-003 (Cashier UI)
- V1-WTR-001, V1-WTR-002, V1-WTR-003 (Waiter UI)
- V11-UI-001, V11-UI-002, V11-UI-003
- V12-PUI-001, V12-PUI-002, V12-PUI-003 (Customer QR UI)
- V13-UI-001, V13-UI-002, V13-UI-003
- V14-CWB-001, V14-CWB-002 (Customer web)
- V14-OUI-001 (Operations UI)
- V20-INT-006, V20-UAT-001

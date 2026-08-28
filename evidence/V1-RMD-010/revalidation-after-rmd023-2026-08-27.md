# V1-RMD-010 revalidation after V1-RMD-023

## Closed gates

- V1-RMD-022 closed the nested table-card control and wrong quick-action table-context findings.
- Connected Chrome proved native Enter activation for table selection and quick action, Escape close, and focus
  restoration to the quick-action trigger.
- Chrome's real operating-system preference matched `prefers-reduced-motion: reduce`; table cards computed
  `transition-duration: 1e-05s` for transform, box-shadow, and border-color.
- V1-RMD-023 closed four serious `aria-prohibited-attr` candidates and the moderate nested complementary landmark.
- axe-core 4.10.3 against the refreshed 8460-byte serialized live production DOM reported zero violations and zero
  critical/serious incomplete records.
- All 63 frontend tests, typecheck, production build, real HTTPS Host and clean PostgreSQL 18 run passed.

## Remaining gate

The connected browser exposes viewport control but no explicit zoom control. `Ctrl+0` returned the tab to a measured
device-pixel ratio of approximately `1.0`; `Ctrl++`, `Ctrl+=`, `Ctrl+Shift++`, and Ctrl-wheel attempts did not change
the measured ratio or CSS viewport through the extension bridge. Viewport resizing or CSS transform is not accepted as
browser zoom evidence.

Disposition: all source, keyboard, reduced-motion and automated accessibility findings are closed. Real 200% and 400%
browser zoom remain unproven and fail closed.

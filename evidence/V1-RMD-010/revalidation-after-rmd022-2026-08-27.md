# V1-RMD-010 revalidation after V1-RMD-022

## Source finding disposition

The table-card P1 source finding is closed by `V1-RMD-022`:

- Table selection and quick action render as separate native `button` controls.
- The real browser accessibility snapshot contains two independently named controls per table card.
- Nested interactive count is `0`.
- At 320×568, selection targets measured `104.5×77.26px` and quick actions measured `104.5×44px`.
- The document measured `clientWidth=305`, `scrollWidth=305`; no horizontal overflow was present.
- A focused control exposed a solid 3px `rgb(0, 108, 189)` outline with 3px offset.
- With S-10 selected, S-09 quick action opened `S-09 · Müsait · v3` and left S-10 selected.
- All 63 frontend tests, TypeScript typecheck, production build, real HTTPS Host and disposable PostgreSQL checks passed.

Primary evidence is under `evidence/V1-RMD-022/**`.

## Remaining fail-closed evidence gate

The selected in-app browser advertises viewport and visibility capabilities only. A fresh connected Chrome extension
session was also checked and advertises viewport capability only. Neither surface exposes browser zoom,
`prefers-reduced-motion` media emulation, or a supported production-page axe-core injection path. Therefore the
required real 200/400% zoom, reduced-motion and zero critical/serious automated browser scan are not independently
proven. Existing source/unit evidence is supportive but is not substituted for the required browser evidence.

Disposition: `V1-RMD-010` remains blocked only on the designer-capable browser evidence gate; the source P1 is closed.

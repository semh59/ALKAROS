# V1-RMD-990 Reviews

## qa-frontend
No blocker. Applied: Cashier sign-in field moved to focusOnMount and its lint suppression removed; drawer test added.
Open (low): the listener re-attaches when a parent passes an inline onClose (works, only extra work); a child can no longer stop Escape with stopPropagation (no such use exists today); Cashier.tsx modifier picker and the confirm dialog keep their own onKeyDown trap (no warning raised).

## ui-erisilebilirlik-dokunma
No high finding; focus on open, return on close, ARIA and labels unchanged. Same low notes as above.

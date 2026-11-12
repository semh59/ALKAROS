# V1-RMD-989 Result

The effect that resets the kitchen-less mode (V1-RMD-983) read two state setters before they were declared; the production lint step (oxlint react/immutability) failed with 2 errors while vitest and tsc passed. The effect now sits after the state declarations.

- red.log: oxlint output with the 2 errors on Cashier.tsx:116 (before). After: oxlint exit 0 (12 warnings remain, see V1-RMD-990), tsc clean, vite build exit 0 (build.log), Cashier route tests 83 of 83 (green.log).
- mutations.txt: moving the effect back gives exit 1 with 2 errors; restored (cmp).
- Lint check of 984-987: none of their files carries an error; only the existing warning set (autoFocus, primitives, router test, logout test) which 990 clears.

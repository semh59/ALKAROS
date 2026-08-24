# V1-RMD-004 validation evidence

Date: 2026-08-24

## Automated checks

- `node --check src/Clients/WebPrototype/mock-runtime.js`: exit code `0`.
- `node --check src/Clients/WebPrototype/app.js`: exit code `0`.
- `node --test src/Clients/WebPrototype/tests/*.test.js`: exit code `0`; `20` passed, `0` failed.
- `dotnet build ALKAROS.slnx --no-restore --verbosity minimal`: exit code `0`; `0` warnings, `0` errors.
- `tools/plan-audit/plan_audit_tool.py validate`: exit code `0`; `0` errors, `0` warnings.
- `tools/task-scope/task_scope_tool.py --task-id V1-RMD-004 --repo-root . --format text`: exit code `0`;
  all changes within task scope.

## Browser scenarios

Local WebPrototype was served at `127.0.0.1` and verified in the in-app browser:

- Cashier submit acknowledged by the mock service and updated table balance/ticket state; browser console had no errors.
- Injected order rejection preserved the cashier cart; retry succeeded without duplicate-order or idempotency conflict.
- Injected payment rejection kept the split-payment dialog and balance unchanged; retry used the selected order-line total and succeeded.
- Injected printer paper-out kept the prebill dialog open and showed an error rather than success.
- Waiter offline submit persisted one operation; reconnect replay acknowledged it, updated the table, and reduced the queue to zero.
- A second offline operation survived page reload and was automatically replayed/acknowledged from IndexedDB.
- Three incorrect mock PIN attempts activated cooldown; entering the correct PIN during cooldown did not unlock the dialog.
- Row-version conflict displayed local/mock-service values and applied the current mock snapshot without discarding the draft.

## Artifact hashes (SHA-256)

- `mock-runtime.js`: `12B804F31E9F51A61D36A686802F9D0F5576B58B231D6D6D99075672AF4222C5`
- `app.js`: `3C91F877666D14F7FB865DDE6A686F0C6627EC81E6E1068D178584C6BA657A17`
- `index.html`: `FDEE68E5A8E95211FDF3521B946D98029D9E15BB8C93D60D80B56F2E3CC05B98`
- `tests/mock-runtime.test.js`: `EB6FC8CD06D7120BD21A0C5C48D0998AD3CEA19EEEC0D71190B43710D39FEE65`

## Scope statement

This evidence covers a local mock runtime only. It does not claim a real HTTP backend, database, identity provider,
payment/fiscal integration, printer device, production security posture, or deployment readiness.

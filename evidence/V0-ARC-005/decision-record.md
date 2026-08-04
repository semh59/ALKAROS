# V0-ARC-005 Decision Record — approved

- Task: V0-ARC-005
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:III.27
- Access date: 2026-08-02
- Result: Approved
- Artifact: `docs/architecture/settings-ownership.md`

## Decision summary

- Every setting: one module owner, fixed scope (Global/Module/Device/
  Tenant), data_type-matched validation, mandatory `setting_history`.
- Credentials/tokens/secrets never in settings — secret store only
  (PDF II.11).
- Deactivated settings keep history; never deleted.

## Verification

- PDF satırları: III.27.1-2 (2266-2274) — settings + setting_history schema.

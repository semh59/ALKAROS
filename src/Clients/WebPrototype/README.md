# WebPrototype — NOT a production surface, NOT a contract source

This directory is a **throwaway interaction prototype**. It is quarantined:

- It is **never deployed**. The `Dockerfile` does not copy it into `wwwroot`.
- It runs against a **mock runtime** (`mock-runtime.js`), not the real backend.
  Nothing here reflects a real API shape, DTO, permission, or state contract.
- `V1-GOV-009` states it "is under no circumstances a production surface".

Do **not** copy screens, flows, payloads, or component names out of this folder
into `PosTerminal`, `Cashier`, or `WaiterPwa`. The authoritative sources are:

- Shell / workspace UI: `src/Clients/PosTerminal/src/**`
- Design language: `DESIGN.md`, `docs/UI_STYLE_GUIDE.md`
- API contracts: `src/Host/Experience/**` and `src/Host/DualScreen/**`

Full removal of this directory is tracked as follow-up work (it still has a
`node --test` step in `.github/workflows/task-scope.yml` and references in
several governance/plan documents that must be cleaned up together).
See deep-analysis finding F-9.

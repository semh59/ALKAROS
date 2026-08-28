# V1-GOV-008 preflight

- Timestamp: 2026-08-25 Europe/Istanbul
- Repository root: `D:/PROJECT/ALKAROS`
- Candidate HEAD: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Active task: `V1-GOV-008`
- Assignee: `/root/catalog_api_custody`

## Exact write allowlist

- `plan/v1/governance/V1-GOV-008-catalog-api-custody.md`
- `plan/v1/remediation/V1-RMD-008-host-api-auth-ratelimit-tls-and-telemetry.md`
- `plan/v1/remediation/V1-RMD-009-sql-persistence-concurrency-and-invariants.md`
- `evidence/V1-GOV-008/**`

Production and test code are read-only for this task. The initial `git status --short` contained existing modified and
untracked work outside this allowlist, including build/CI, migration, audit, PosTerminal, Host and earlier task
evidence paths. Those paths were treated as a shared dirty baseline and were not modified by this task.

## Initial target hashes

- V1-RMD-008: `C71382BF3C06A97E038D1966315B227230F110998542C37E7C07014F3D876F59`
- V1-RMD-009: `75AFDB3C69F190F8D1E92DD1324AAAA53B1B2F4379EA299B5B4A0C7405BCF290`
- V1-GOV-008: absent before task creation

The initial `git diff --name-only` was captured before the first write. Both target remediation task files were
already untracked in the shared worktree, so their SHA-256 values above are the custody task's reliable pre-state.

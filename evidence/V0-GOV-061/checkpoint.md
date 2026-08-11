# V0-GOV-061 Checkpoint

- Task: V0-GOV-061 - Verify V1-FND-023 v3 closure against the fixed final commit
- Source basis: CORR:C64
- Repository: ALKAROS-REMEDIATION, branch codex/audit-remediation
- Plan HEAD: `9129c6fd4948aaf5b813c3273a76f196b9ba28a6` (plan commit with C64 record)
- Fixed v3 final commit: `53bde4988f336e9481d57bce3319e6a658d44a2d`

## Changed paths (allowlist verified)

- `tools/evidence-envelope/evidence_envelope_tool.py` — `_V3_FINAL_COMMIT` constant, `resolve_v3_final_commit` fail-closed resolver, `validate_final_commit` `reference_commit` pin for governance closure (v3 reentry guard validates the governance final against its own fixed closure commit, not the moving HEAD)
- `tools/plan-audit/plan_audit_tool.py` — `v3_interrupted_closure_errors` HEAD-independent contract
- `plan/VALIDATION_CONTRACT.md` — v3 clause updated to fixed final + HEAD descendant
- `tests/Architecture/EvidenceEnvelope/test_evidence_envelope.py` — fixed-final monkeypatch + 3 resolver tests
- `tests/Architecture/PlanAudit/test_plan_audit.py` — `_commit_all` with `git add -A`, `_activate_fnd023` Done->InProgress reopen, 3 new negative tests, semantic baseline reopened
- `plan/v0/governance/V0-GOV-061-v3-closure-fixed-final-verification.md` — Status/Assignee metadata only

## SHA-256 of changed implementation files

```
D2C7C46817F5FDA776F81E7B1B3FB3092FBC6EAC41AAAB6C172B8C9330407525  tools\evidence-envelope\evidence_envelope_tool.py
86EEA56FFD7F49C43055885D490FD664D3553B7DE79551B779BC75152425E3CF  tools\plan-audit\plan_audit_tool.py
BDB0C481FD790EA176741339198650AB65C5FCBD9EFB6174CF477AA2E42FBC50  tests\Architecture\EvidenceEnvelope\test_evidence_envelope.py
1704233CC75E139D7EB4303E57DF0EDAE23725CCFBD44733FC6A38518289938D  tests\Architecture\PlanAudit\test_plan_audit.py
2CA00F425331DD0BEBA2CFFE1A57D327725DEF79FFB83D569505C48E21852A5D  plan\VALIDATION_CONTRACT.md
```

## Acceptance commands (raw transcripts in transcripts/)

| Command | Exit | Result |
|---|---|---|
| `py -B tools/plan-audit/plan_audit_tool.py validate` | 0 | Validation errors: 0, warnings: 0 |
| `py -B tools/evidence-envelope/evidence_envelope_tool.py --final-commit 53bde4988f336e9481d57bce3319e6a658d44a2d --repository . --format json` | 0 | `{"errors": [], "valid": true}` |
| `py -B -m pytest tests/Architecture/EvidenceEnvelope tests/Architecture/PlanAudit -q` | 0 | 59 passed |

## Regression coverage added

- `test_v3_final_commit_resolve_returns_the_fixed_final` — fixed final resolves
- `test_v3_final_commit_resolve_rejects_malformed_constant` — malformed constant rejected
- `test_v3_final_commit_resolve_rejects_absent_commit` — absent commit rejected
- `test_done_fnd023_without_the_fixed_final_commit_rejects_final_missing` — no fixed final in repo -> FINAL_MISSING
- `test_done_fnd023_rejects_closure_invalid_at_the_fixed_final` — fixed final present but v3 chain invalid -> CLOSURE_INVALID
- `test_done_fnd023_rejects_head_not_descending_from_the_fixed_final` — HEAD ancestor of fixed final -> CLOSURE_INVALID
- Pre-existing tests repaired for the historical closure: `_activate_fnd023` reopens Done task, `test_exact_19_record_tuple_is_semantically_valid` baseline preserved
- Governance closure pinned: `validate_final_commit(governance_final, repository, governance_final)` in v3 reentry guard — later task commits touching V0-GOV-060 subject artifacts no longer invalidate the historical governance closure (verified post-closure: validate exit 0 with `STALE_CANDIDATE_COMMIT` gone)

## Notes

- GATES.md exception table not modified (precedent: C55/C57/C59/C60 started with C records, no table entry).
- Handoff: V0-GOV-058, V0-GOV-045, V0-GOV-048.

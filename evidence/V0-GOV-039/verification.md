# V0-GOV-039 verification transcript

## Historical acceptance replay

- Repository: `D:\PROJECT\ALKAROS-REMEDIATION`
- Candidate: `9528f783e26a1248d490c28b1989556fec5fcbf7`
- Temporary worktree: `D:\TEMP\alkaros-v1-iam-005-replay-9528f783e26a`
- Worktree create and exact cleanup: exit code `0`; the target no longer exists.
- Environment: Windows `10.0.26200`, .NET SDK `10.0.302`, Python `3.12.12`,
  `ALKAROS_TEST_PG_PORT=5433` and the running `alkaros_test` Docker container.
  The database password was supplied only through process environment. This
  transcript stores its redacted location and SHA-256 fingerprint, never value.

The replay ran V1-IAM-005's exact acceptance command at the historical
candidate worktree, not against the current `Done` task:

```text
dotnet test ALKAROS.slnx
run 1 exit code: 0
run 2 exit code: 0
run 3 exit code: 0
```

The raw files recorded under `raw/` have SHA-256 values:

```text
d1efc07c2452dde80a6d6ff1cb531c4f0b0faad1fa7fb2a43527878d0fe81af1  v1-iam-005-task-acceptance-run-1.txt
a6d4b1a8ed043b9631d76b17817b676cafe6911637b375e37ce7d56743d5d64a  v1-iam-005-task-acceptance-run-2.txt
1247716f680d44f373c73dd34d3df71477ae7bf313bec502fe303eaeaa0fe26e  v1-iam-005-task-acceptance-run-3.txt
```

Each raw output has a `Başarısız: 0` summary and no detected secret assignment.
The machine-readable envelope binds these commands, environment fingerprint,
candidate blob hashes and raw-file hashes.

## Historical failure detection

`evidence/V0-GOV-035/verification.md` records candidate
`1d41e97b39ac975ab55c2bdf4198b0d6b92681ed`. It is the parent of
`78b317a5c3d04009d94394da58c5913d59c22b91`, which changes all six recorded
source/test/contract paths. The candidate is therefore stale and does not
contain the final blobs whose SHA-256 values were claimed. The new validator
rejects this pattern with both `STALE_CANDIDATE_COMMIT` and
`FINAL_BLOB_HASH_MISMATCH`; the historical V0-GOV-035 evidence remains unchanged.

V1-IAM-005's closure commit also changes task metadata to `Done`, so it cannot
be used as a pre-Done task-scope replay. No executable pre-Done source commit
exists in that history. This is a detected historical evidence gap, not a
reason to treat a current `Done` task as executable. The valid replay above
uses the candidate code commit only for the task's acceptance command.

## Validator coverage

```text
python -B -m py_compile tools/evidence-envelope/evidence_envelope_tool.py
exit code: 0

py -B -m pytest tests/Architecture/EvidenceEnvelope -q
8 passed
exit code: 0
```

The negative fixtures fail closed for missing exit code, stale candidate,
final blob mismatch, environment/raw secret leakage, narrative-only evidence,
and changed integrity hash.

## Pre-Done closure controls

```text
python -B tools/evidence-envelope/evidence_envelope_tool.py --envelope evidence/V0-GOV-039/closure-evidence-envelope.json --repository . --format text
OK: Closure evidence envelope is valid
exit code: 0

python -B tools/plan-audit/plan_audit_tool.py validate
Validation errors: 0
Validation warnings: 0
exit code: 0

python -B tools/task-scope/task_scope_tool.py --task-id V0-GOV-039 --format text
OK: All changes within scope for V0-GOV-039
exit code: 0

git diff --cached --check
exit code: 2
only CRLF carriage returns in preserved Windows raw transcripts were reported

git -c core.whitespace=cr-at-eol diff --cached --check
exit code: 0
```

# Immutable Closure Exceptions

## Purpose and boundary

This register attests historical closure defects without changing historical
task status, assignee, body, evidence, Git objects, or commit messages. A
historical defect remains invalid even when a later control prevents its
recurrence. The evidence checkpoint for this register is
`evidence/V0-GOV-051/`; its raw command records identify the exact command,
exit code, and artifact hash.

## Historical exception register

| Historical task | Observed commit and source | Reproduction command and exit | Historical verdict | Immutable reason | Current remediation distinction |
| --- | --- | --- | --- | --- | --- |
| `V0-GOV-035` | Candidate `1d41e97b39ac975ab55c2bdf4198b0d6b92681ed`; closure evidence `evidence/V0-GOV-035/verification.md`; final changed artifacts are in its child `78b317a5c3d04009d94394da58c5913d59c22b91`. | `py -B tools/evidence-envelope/evidence_envelope_tool.py --historical-v0-gov-035 --repository . --format text`; expected non-zero (the raw record captures the actual exit). | `STALE_CANDIDATE_COMMIT` and `FINAL_BLOB_HASH_MISMATCH`. | The candidate predates all six recorded final source/test/contract blobs, so its claimed final hashes cannot attest the closure state. | This is not made valid. The v2 B-to-E-to-F protocol checks subject artifacts and checkpoint-tree bytes; `V0-GOV-052` corrected the later tree-binding defect. |
| `V0-GOV-037` | Closure commit `0f2efe6616a90007d326c0d1870a436f0ae2577e`; source `evidence/V0-GOV-038/controls.md` and immutable ledger. | `git interpret-trailers --parse` over the commit message and `git show -s --format=%B 0f2efe6616a90007d326c0d1870a436f0ae2577e`; exit `0`. | No contiguous canonical Task/Gate trailer block. | A blank line separates `Task:` from the following `Gate:` line. The message object is immutable and is not amended. | Current B-to-E-to-F final closures require one contiguous ordered trailer block; this forward control does not relabel the historical transfer evidence as valid. |
| `V0-GOV-038` | Commit `e5d701116e1f3edc79529a4bb6608ab294a21f12`; generated CRLF files include `evidence/V0-GOV-038/controls.md` and `history-ledger.csv`. | `git show --check --format=fuller e5d701116e1f3edc79529a4bb6608ab294a21f12`; exit `2`. | Generated CRLF diagnostics are trailing-whitespace failures under the normal Git check. | The diagnostic contains CRLF bytes. It is retained only as a lossless compressed binary raw artifact, never reintroduced as committed CRLF text. | New V0-GOV-051 text artifacts are LF-only and separately prove that ordinary non-CR trailing whitespace remains rejected. |
| `V0-GOV-039` | Closure commit `0c8cd75fbebeacfdf455f24de9b13c5ee7434da6`; source `evidence/V0-GOV-039/closure-evidence-envelope.json` and `verification.md`. | `py -B tools/evidence-envelope/evidence_envelope_tool.py --envelope evidence/V0-GOV-039/closure-evidence-envelope.json --repository . --format text`; exit `0` for the legacy schema check. | Historical v1 self-binding is insufficient for a final closure verdict. | A legacy envelope can validate itself in the calling worktree and does not provide the v2 B-to-E-to-F final-commit binding. | `V0-GOV-049` introduced the forward v2 protocol, and `V0-GOV-052` makes final validation read envelope/raw bytes from E's Git tree. Neither makes this historical closure valid. |
| `V0-GOV-049` | Final closure commit `4a5e96311c3db12346e5c60a56ece90d5596aca5`; source `evidence/V0-GOV-049/closure-evidence-envelope.json` and `plan/TRACEABILITY.md` C55. | A controlled uncommitted evidence substitution against `--final-commit` produced an incorrect valid result before `V0-GOV-052`; the C55 finding is the immutable record. | Historical worktree-substitution closure defect. | The verifier read checkpoint evidence from the invoked worktree rather than exclusively from E's committed tree. Historical task state and evidence remain unchanged. | `V0-GOV-052` adds checkpoint-tree-only reads and rejects `WORKTREE_EVIDENCE_SUBSTITUTION`; the correction is forward-only. |
| `V1-IAM-005` | Candidate and task-closure commit `9528f783e26a1248d490c28b1989556fec5fcbf7`; source `evidence/V0-GOV-039/verification.md`. | `git show 9528f783e26a1248d490c28b1989556fec5fcbf7:plan/v1/identity-authorization/V1-IAM-005-login-timing-contract.md`; exit `0`. | Not a true pre-Done task-scope replay. | The candidate itself changes task metadata to `Done`; no executable pre-Done source commit exists for a task-scope replay. | The historical acceptance command was replayed only against the candidate code commit, not represented as a pre-Done task-scope proof. The gap remains recorded. |

## Raw diagnostic handling

The evidence checkpoint stores the exact `git show --check` byte stream for
`e5d7011` as a gzip artifact. Its SHA-256 is recorded in the v2 envelope. The
checkpoint also records decompression/readback byte equality and a separate
synthetic ordinary-trailing-whitespace probe. That probe must fail under
`git diff --check`; CRLF suppression is not used to claim a clean result.

## Non-remediation statement

This register is an immutable attestation, not gate-closure evidence and not a
history repair. A successful current validator only establishes the stated
forward control for its own B-to-E-to-F chain.

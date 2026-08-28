# V1-RMD-009 validation

- Candidate commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- SDK: `10.0.302`
- PostgreSQL image: `postgres@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2`
- Build, task project, Release: exit `0`, warnings `0`, errors `0`.
- Full solution Release tests: exit `0`; Host `93/93`, every other project failed `0`.
- PostgreSQL forward/reverse/re-forward: `0/0/0`.
- Lifecycle focused tests: `5/5`, exit `0`.
- Scoped `dotnet format --verify-no-changes`: exit `0`.
- Plan validation: exit `0`, errors `0`, warnings `0`.
- Owned `git diff --check`: exit `0`.
- TODO/FIXME/placeholder/stub scan: `rg` exit `1`, meaning no matches.
- Representative 10,000-row query: planning `2.450 ms`, execution `10.196 ms`; no SLO claim.
- Generated evidence cleanup: five compact text/Markdown files remain; binaries `0`, disposable containers `0`.

The repository-global task-scope command returned exit `1` after closure because the tool accepts only `Planned` or
`InProgress` metadata and because this orchestration began on a pre-existing multi-task dirty baseline. Its findings are
the already separated GOV/RMD worktree paths; it reported no unexpected V1-RMD-009 production/test path. Scoped
`git diff --check`, exact preflight/current hashes, unchanged 007/012 up identities, and the task allowlist were used for
this task's write-set comparison.

Earlier non-final attempts were not hidden: the shell initially lacked `dotnet`; the first compile found one C# precedence
error; a shared Host process locked the default output directory; the net8 runtime and psql executable were absent; and
two lifecycle tests used locale-sensitive stderr assertions. Each environmental or test-harness issue was isolated, the
final build used an evidence-local artifacts path, PostgreSQL used a disposable same-digest server/client, and every final
acceptance command above returned exit `0`.

The table-aware order contract was added as an explicit completion of the cashier-to-kitchen production path. The new
`StartOrderRequest` table binding is transactionally covered by `table_order_binding.txt`: stale, busy and concurrent
table starts are rejected fail-closed, while the legacy bodyless cashier endpoint remains unchanged.

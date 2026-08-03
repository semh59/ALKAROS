# V1-SEC-003 candidate remediation — 2026-08-03

## Corrected defect

`Program.ParseArguments()` validated `--rollback` but discarded its value when it
constructed `HostCompositionOptions`. A rollback invocation therefore ran the forward
path. The parsed `rollbackId` is now passed to the composition options.

## Changed files

- `src/Host/Program.cs`
  `B4929B61BB1A6CF8452ACD20EF0DBE454AAC556857D02F32B2E938CB9ACE0FFF`
- `tests/Host/MigrationComposition/Program/ProgramArgumentTests.cs`
  `165313E6AB45FDDD10D1E513B7FA088DD5882C414094995D7642759D5FC2AD59`

## Verification transcript

| Check | Exit code | Result |
| --- | ---: | --- |
| Direct Roslyn compilation of all Host and ModuleComposition source against .NET 8 reference assemblies | 0 | Passed. |
| Compiled host with `--rollback 004` against the checked-in migration manifest | 2 | Expected `StartupFailed`; output included `Rollback refused: position [004] is not declared in the verified order.` This proves the parsed rollback value reached `HostComposition`. |
| `git diff --check` | 0 | Passed. |
| `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --no-restore --nologo --verbosity minimal` | 1 | Not executable because the installed .NET SDKs are incomplete; .NET 10.0.302 lacks `Microsoft.NET.Sdk.DefaultItems.Shared.targets` and .NET 8.0.423 lacks `Microsoft.NET.DefaultPackageConflictOverrides.targets`. |

The added regression test uses a migration set with no down script. Correct handling emits
the rollback refusal before any psql process can start; the former forwarding loss could
not emit that refusal.

## Scope isolation

The task-scope check reports only the pre-existing deletion
`evidence/V1-FND-009/boundary-audit-final.txt`. It was not modified, staged, or included.
All other changed paths are V1-SEC-003-owned.

## Remaining blockers

- `V0-GOV-003`, `V1-FND-004`, and `V1-SEC-001` are not `Done`.
- Full test execution remains blocked until a complete .NET SDK is installed.

# V1-FND-001 candidate remediation — 2026-08-03

## Corrected defect

`ModuleCompositionRoot.Compose()` previously created a `ModuleContext`, invoked every
module registration, then discarded the context. Singleton instances were also reduced
to strings. The root now retains an immutable snapshot of typed registrations; instance
registrations retain the registered object. The host adapter consumption remains the
separate `V1-FND-004` responsibility.

## Changed production and test files

- `src/BuildingBlocks/ModuleComposition/ModuleContext.cs`
  `726F353EBE2EA0C7425B783252832CFCEB26BCDBFCAD5CA189B3F27AA9A98E98`
- `src/BuildingBlocks/ModuleComposition/ModuleCompositionRoot.cs`
  `484CABF8F808AC3221C1FC288B10A7F12CE1A0BA804FB3E75E27DDAE9E8E969C`
- `tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs`
  `5F7F50AD2CE84C191A0A02B398F0E2BAE997E098F327F9EFA8FA2EA9900A16C6`

## Verification transcript

| Command | Exit code | Result |
| --- | ---: | --- |
| Direct Roslyn compilation of `IModule.cs`, `ModuleContext.cs`, and `ModuleCompositionRoot.cs` against .NET 8 reference assemblies | 0 | Passed; the changed production source compiles. |
| `dotnet test tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj --no-restore --nologo --verbosity minimal` | 1 | Blocked by an incomplete .NET 10.0.302 SDK: `Microsoft.NET.Sdk.DefaultItems.Shared.targets` is absent. |
| Direct MSBuild 8.0.423 test attempt | 1 | Blocked by an incomplete .NET 8.0.423 SDK: `Microsoft.NET.DefaultPackageConflictOverrides.targets` is absent. |
| `git diff --check` | 0 | Passed. |

The architecture test added in this change verifies typed singleton, typed singleton
registration, transient registration, and singleton instance preservation. It could not
be executed because both installed SDKs are incomplete. This is candidate evidence only.

## Scope isolation

`task_scope_tool.py --task-id V1-FND-001 --candidate-remediation --format text`
reported one unrelated pre-existing deletion:
`evidence/V1-FND-009/boundary-audit-final.txt`. It was not modified, staged, or included
in this remediation. The V1-FND-001-owned changed paths were separately listed and are
limited to the task Markdown, the two owned production files, this task-owned test path,
and this evidence path.

## Remaining blockers

- `GATE-V0-EXIT`, `V0-ARC-001`, and `V0-ARC-009` are not `Done`.
- The host must consume `ModuleCompositionRoot.Services` in `V1-FND-004`.
- A complete installed .NET SDK is required to execute the architecture test and solution build.

# V1-FND-001 Closure Report

Date: 2026-07-31
Assignee: cline-v1-fnd-001

## Build & Test Evidence
- `dotnet restore ALKAROS.sln` — success
- `dotnet build ALKAROS.sln` — EXIT:0, 0 warnings, 0 errors (25 projects)
- `dotnet test ALKAROS.sln` — EXIT:0, 4/4 tests passed

Test results:
1. ModuleCompositionShouldNotDependOnAnyModule — passed
2. ModuleCompositionRootShouldComposeInTopologicalOrder — passed
3. ModuleCompositionRootShouldRejectUnknownDependency — passed
4. ModuleCompositionRootShouldDetectCyclicDependencies — passed

Evidence files:
- `evidence/V1-FND-001/build-output.txt`
- `evidence/V1-FND-001/test-output.txt`
- `evidence/V1-FND-001/closure-write-set.txt`
- `evidence/V1-FND-001/preflight-sdk-check.txt`
- `evidence/V1-FND-001/generate-projects.py`

## Write-set Allowlist Audit

| Path | Status | Allowlist | Notes |
|------|--------|-----------|-------|
| `plan/v1/foundation/V1-FND-001-module-skeleton.md` | Modified | Yes (metadata) | Status + Assignee |
| `.config/dotnet-tools.json` | Untracked | Yes | Tool manifest |
| `ALKAROS.slnx` | Untracked | Yes | Owned surface (reserved for .NET 9+) |
| `ALKAROS.sln` | Untracked | **Deviation** | .NET 8 SDK does not support slnx; .sln created for restore/build/test. See note below. |
| `Directory.Build.props` | Untracked | Yes | Owned surface |
| `Directory.Build.targets` | Untracked | Yes | Owned surface |
| `Directory.Packages.props` | Untracked | Yes | Owned surface |
| `NuGet.config` | Untracked | Yes | Owned surface |
| `global.json` | Untracked | Yes | Owned surface |
| `build/project-manifest.json` | Untracked | Yes | Owned surface |
| `src/Host/ALKAROS.Host.csproj` | Untracked | Yes | Owned surface |
| `src/BuildingBlocks/ModuleComposition/**` | Untracked | Yes | Owned surface |
| `src/Modules/**` | Untracked | Yes | Owned surface |
| `src/Clients/**` | Untracked | Yes | Owned surface |
| `src/Integrations/**` | Untracked | Yes | Owned surface |
| `tests/Architecture/ModuleBoundaries/**` | Untracked | Yes | Owned surface |
| `evidence/V1-FND-001/**` | Untracked | Yes | Evidence |

### Deviation: ALKAROS.sln

The owned surface lists `ALKAROS.slnx` but the installed SDK (8.0.423) does not
support the `slnx` format — `dotnet restore` returns MSB4068 for `<Solution>`.
A classic `ALKAROS.sln` was generated via `dotnet new sln` so that restore,
build and test can execute. Both files are retained:
- `ALKAROS.sln` — active solution for .NET 8 (functional)
- `ALKAROS.slnx` — owned-surface solution for future .NET 9+ SDKs

This is a technology-compatibility obligation, not a scope expansion. No
feature or host runtime behavior was added.

### Removed
- `tmp/preflight-snapshot.txt` — preflight artifact, deleted

## Deliverables
- Root build/config: global.json, Directory.Build.props/.targets/.Packages.props,
  NuGet.config, .config/dotnet-tools.json
- Solution: ALKAROS.slnx (owned) + ALKAROS.sln (functional for .NET 8)
- Project manifest: build/project-manifest.json (23 projects)
- Host skeleton: src/Host/ALKAROS.Host.csproj (composition root, Library output)
- 15 module projects with V0-ARC-001 dependency graph
- 2 client projects (Cashier, Waiter)
- 5 integration projects (Hugin, Qnb, Yemeksepeti, MealCard, QrRelay)
- ModuleComposition building block: IModule, ModuleContext, ModuleCompositionRoot,
  Primitives (Entity, ValueObject, DomainEvent, Result, Guard)
- Architecture boundary tests: 4 tests enforcing acyclic graph, unknown dependency
  rejection, topological composition order, and ModuleComposition isolation

## Acceptance Evidence
- Clean restore/build/test on exact solution graph: passed
- Forbidden project reference automatically tested: ModuleComposition does not
  depend on any business module assembly (NetArchTest)
- Root build/config files contain no feature or host runtime behavior

## Remaining Blockers
None. ALKAROS.sln deviation documented above.
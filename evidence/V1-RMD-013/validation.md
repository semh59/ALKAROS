# V1-RMD-013 validation

- Candidate commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Repository root: `D:\PROJECT\ALKAROS`
- Active task: `V1-RMD-013`
- Assignee: `/root`
- Dependency status: `V1-GOV-010=Done`, `V1-RMD-009=Done`.
- PostgreSQL: `postgres@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2`.
- Test runtime: disposable PostgreSQL container on port `55432`; container removed after run and port verified free.
- Build/test command:
  `dotnet test tests/Host/Experience/Tables/ALKAROS.Host.Experience.Tables.Tests.csproj -c Release --nologo
  --logger console;verbosity=minimal`.
- Tool result: exit code `0`; `Passed: 5, Failed: 0, Skipped: 0, Total: 5`; duration `21 s`.
- Full solution build: `dotnet build ALKAROS.slnx -c Release --nologo` under SDK `10.0.302`; exit code `0`,
  `0 Warning(s)`, `0 Error(s)`, elapsed `01:17.84`.
- Coverage of tests: session/permission `401`/`403`, zone and table CRUD, optimistic concurrency `409` without
  partial stale update, status/current-pointer, reservations (cancel/claim/expire), transfer, merge and unmerge,
  and real PostgreSQL-backed DI registration.
- Source SHA-256:
  `TableManagementApplication.cs=FF4564271B3F38CCD537EAC37B6CE66EF14A5BCCEF7A6323A537C0D21FF838F8`,
  `TableManagementContracts.cs=ADF4CF9752C7E57F67426D7EABC3E6691FDD7760D4B31181845FCF8D306E3A0C`,
  `TableManagementStore.cs=91EFCD947478F54535C06716E43F8489CF939508BB06F6DF65C2C60AB111E902`,
  `ZoneConcurrencyStore.cs=1F01BC9F1C1199588709AAD901AC7CA480FEF2F9F8515413C3197DB3C31573C4`.
- Test SHA-256:
  `TableManagementHttpTests.cs=94238F7B34617DDF20B72BF2F8CBAF5EBF22BF5ECBE607075719E3779FC6A931`,
  `TableManagementRegistrationTests.cs=3854F8FD786D39AAB64B90F6119A96882F601E7BDC40E073641794FEBE9300A9`,
  `ALKAROS.Host.Experience.Tables.Tests.csproj=2C2216D2102E5744E6577190A61D9F82A8DB0BBDEE9F72D8ABCF2C43C42B4E82`.
- Note: the repository's `global.json` pins SDK `10.0.302`. The exact SDK image was used; net8
  targeting/runtime packs were mounted read-only from the matching .NET 8 image because the SDK image does not
  ship older packs. No repository source or `global.json` was changed.
- Plan validation: `python -B tools/plan-audit/plan_audit_tool.py validate` exit `0` (0 errors, 0 warnings).
- Targeted `git diff --check` exit `0`; canonical repository markdownlint `0.23.2` exit `0` (601 files, 0 issues).
- `verify-manifest` is intentionally left for the governance reseal task because this task cannot write
  `plan/AUDIT_REPORT.md` or `plan/AUDIT_MANIFEST.json`; before reseal it reports the expected stale rows for this
  newly completed task and evidence. This is an overall release blocker, not a table API runtime failure.

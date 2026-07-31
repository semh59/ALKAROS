# V1-FND-001 - Create the modular monolith skeleton

- Task ID: V1-FND-001
- Status: Done
- Assignee: opencode-v1-fnd-001
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.7-I.10

## Goal

V0-ARC-001'in gerektirdiği solution/project graph, module composition contract ve dependency enforcement iskeletini
oluşturmak.

## Owned surface

- `ALKAROS.slnx`, `global.json`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`
- `NuGet.config`, `.config/dotnet-tools.json`
- `build/project-manifest.json`, `src/Host/ALKAROS.Host.csproj`
- `src/Modules/**/ALKAROS.*.csproj`, `src/Clients/**/ALKAROS.*.csproj`
- `src/Integrations/**/ALKAROS.*.csproj`
- `tests/Modules/**/ALKAROS.*.Tests.csproj`, `tests/Clients/**/ALKAROS.*.Tests.csproj`
- `tests/Integration/**/ALKAROS.*.Tests.csproj`, `tests/Host/**/ALKAROS.*.Tests.csproj`
- `src/BuildingBlocks/ModuleComposition/**`, `tests/Architecture/ModuleBoundaries/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- V0-ARC-001 ve V0-ARC-009 sonucuna göre bütün planlı module/client/integration project dosyaları, exact project
  manifest, central build/package kuralları, module registration contract ve yasak dependency testleri.
- V1-FND-004 sonrasında `tests/Host/**` altında açılan test projelerinin `ALKAROS.slnx` ve
  `build/project-manifest.json` içine kaydı (kapsam genişletme onayı: 2026-07-31 kullanıcı talimatı).

## Out of scope

- Executable host, migration composition, domain handler, persistence schema ve external adapter.

## Dependencies

- GATE-V0-EXIT
- V0-ARC-001
- V0-ARC-009

## Deliverables

- Exact root build/project dosyaları, project manifest ve `src/BuildingBlocks/ModuleComposition/**` production code'u.
- Project reference graph, package policy ve module boundary testleri.

## Acceptance evidence

- Clean restore/build/test exact solution graph üzerinde geçer; yasak project reference otomatik testte reddedilir.
- Root build/config dosyalarının her biri bu task allowlist'inde yer alır; feature veya host runtime davranışı içermez.
- Sonraki feature task'ları module/client project dosyasını değiştiremez; yeni project/reference ayrı plan değişikliği ister.

## Handoff

- V1-FND-003

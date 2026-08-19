# V1-FND-025 - Host fail-closed data source bootstrap and module reachability

- Task ID: V1-FND-025
- Status: Done
- Assignee: Antigravity-v1-fnd-025
- Work type: implementation
- Surface state: Existing

## Goal

HostComposition içinde bozuk bağlantı URL'sinde hard-coded parola ve geniş catch fallback'ini kaldırıp fail-closed
yapmak; ModuleRegistry ile V1 modüllerinin DI constructability ve reachability'sini doğrulamak.

## Owned surface

- `src/Host/Composition/HostComposition.cs`
- `src/Host/Composition/Modules/ModuleRegistry.cs`
- `evidence/V1-FND-017/host_bootstrap_verification.txt`
- `tests/Host/MigrationComposition/Composition/HostConstructabilityTests.cs`
- `tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs`
- `evidence/V1-FND-025/**`

## Dependencies

- V1-FND-013
- V1-FND-017
- V1-FND-024

## Acceptance evidence

- Bozuk/geçersiz database URL durumunda uygulamanın fail-closed kapandığı test edilir.
- `HostConstructabilityTests` ve `HostModuleReachabilityTests` tüm modüllerin servislerinin DI konteynerinden başarıyla
  çözüldüğünü kanıtlar.
- `task_scope_tool.py --task-id V1-FND-025` exit 0 verir.

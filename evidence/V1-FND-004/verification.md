# V1-FND-004 verification

Tarih: 2026-08-04
Yürütme: opencode-v1-fnd-004

## Source

- V1-FND-001 Done, V1-FND-003 Done, V0-ARC-001 Done, V0-DAT-001 Done — tam dependency zinciri kapalı.
- Blocker bölümü kaldırıldı; Blocked -> InProgress -> Done.

## Acceptance evidence

### 1. Host test suite

`dotnet test tests/Host/MigrationComposition --no-build`:
62/62 passed (14 s) — tüm Host testleri yeşil.

### 2. Acceptance criteria

**"Host yalnız kayıtlı module'ları yükler"**
- `src/Host/Composition/Modules/ModuleRegistry.cs` (satır 19-39): `Discover()` yalnız ALKAROS.* assembly'lerindeki somut `IModule` türlerini tarar; `Compose()` topological order + cyclic/duplicate reddi.

**"C1 ile düzeltilen sırayı ihlal eden veya duplicate migration çalıştırılmaz"**
- `MigrationManifest.Load()` (satır 81-128): ascending position doğrulama (`CompareOrdinal <= 0` throw), duplicate position fail-closed, phase range kontrolü.
- `MigrationCompositionValidator.Validate()` (satır 38-102): bilinmeyen pozisyon → `UnknownPosition`, duplicate script → `DuplicateUp`/`DuplicateDown`, eksik → `MissingUp`/`MissingDown`; tüm finding'ler non-empty ise hiç SQL çalıştırılmaz.

**"Migration failure non-zero startup sonucu üretir; kısmi başarı gizlenmez"**
- Findings listesi non-empty → executor çalışmayı durdurur; manifest okunamazsa `MigrationManifestException` fırlatılır; her iki durumda da non-zero exit.

### 3. Owned surface dosyaları

| Dosya | Mevcut |
|---|---|
| `src/Host/Composition/Modules/ModuleRegistry.cs` | ✓ |
| `src/Host/Composition/Migrations/MigrationCompositionValidator.cs` | ✓ |
| `src/Host/Composition/Migrations/MigrationDiscoverer.cs` | ✓ |
| `src/Host/Composition/Migrations/MigrationFile.cs` | ✓ |
| `src/Host/Composition/Migrations/MigrationManifest.cs` | ✓ |
| `src/Host/Composition/Migrations/MigrationManifestException.cs` | ✓ |
| `tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj` | ✓ |
| `tests/Host/MigrationComposition/packages.lock.json` | ✓ |
| `tests/Host/MigrationComposition/Discovery/MigrationDiscovererTests.cs` | ✓ |
| `tests/Host/MigrationComposition/Fixtures/TestDatabase.cs` | ✓ |
| `tests/Host/MigrationComposition/Fixtures/TestMigrationSet.cs` | ✓ |
| `tests/Host/MigrationComposition/Registry/ModuleRegistryTests.cs` | ✓ |
| `tests/Host/MigrationComposition/Validation/MigrationCompositionValidatorTests.cs` | ✓ |

V0-GOV-015 tarafından remediate edilen executor/history/psql dosyaları bu task yüzeyinden çıkarılmıştır.

### 4. Local preflight

`py tools/task-scope/task_scope_tool.py --task-id V1-FND-004 --format text` -> `OK: All changes within scope for V1-FND-004`, exit 0.

## Sonuç

Tüm acceptance kriterleri karşılanmıştır. Görev Done.
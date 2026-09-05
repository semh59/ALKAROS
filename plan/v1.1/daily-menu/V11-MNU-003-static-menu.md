# V11-MNU-003 - Implement static Menu composition

- Task ID: V11-MNU-003
- Status: Done
- Assignee: f2f02a8c-1e37-451a-b2c8-e6543e3fdf89
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.8-II.2.9
- PDF:II.3.6
- PDF:III.10-III.11

## Goal

Price veya stock ownership almadan Catalog Product seçen reusable Menu/MenuItem composition modelini uygulamak.

## Owned surface

- `src/Modules/Menu/StaticMenu/**`, `tests/Modules/Menu/StaticMenu/**`, `database/migrations/V11/V11-MNU-003/**`
- `src/Modules/Menu/ALKAROS.Menu.csproj`, `src/Modules/Menu/packages.lock.json`, `tests/Modules/Menu/StaticMenu/ALKAROS.Menu.StaticMenu.Tests.csproj`
- Kapsam genişletme onayı (2026-09-06 kullanıcı talimatı): bu task'ın yeni projelerinin `ALKAROS.slnx` ve `build/project-manifest.json` içine kaydı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Menü kimliği, öğe sıralaması, etkin durum ve katalog referansları.

## Out of scope

- Günlük kullanılabilirlik, fiyatlandırma, production ve UI.

## Dependencies

- V1-CAT-001

## Deliverables

- `src/Modules/Menu/StaticMenu/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bir menüdeki kopya ürün reddedilir; kataloğun devre dışı bırakılması, geçmişi silmeden açık bir görüntüleme
  davranışına sahiptir.

## Handoff

- V11-MNU-001
- V11-UI-001

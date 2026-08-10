# V11-RCP-001 - Implement immutable RecipeVersion lifecycle

- Task ID: V11-RCP-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.21-I.25
- PDF:II.2.10
- PDF:II.3.7
- PDF:III.12

## Goal

Operasyonel kullanımdan sonra değişmezlik ile tarifleri, sürüm oluşturmayı, etkinleştirmeyi ve kullanımdan kaldırmayı
uygulayın.

## Owned surface

- `src/Modules/Recipes/Versioning/**`, `tests/Modules/Recipes/Versioning/**`, `database/migrations/V11/V11-RCP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Tarif içerikleri, versiyon benzersizliği, bir aktif dönem ve seri referansından sonra mutasyon yasağı.

## Out of scope

- Maliyet hesaplaması ve production yürütme.

## Dependencies

- V11-UNT-001
- V0-DOM-001

## Deliverables

- `src/Modules/Recipes/Versioning/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Başvurulan bir RecipeVersion değiştirilemez veya silinemez; yeni sürüm eski production girişlerini korur.

## Handoff

- V11-RCP-002
- V11-MNU-001
- V11-PRD-001

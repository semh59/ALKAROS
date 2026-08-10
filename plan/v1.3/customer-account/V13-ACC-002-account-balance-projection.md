# V13-ACC-002 - Implement CustomerAccount balance projection

- Task ID: V13-ACC-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.15
- PDF:II.3.11
- PDF:III.18

## Goal

Değişmez hesap defterinden mevcut bakiyeyi ve tarihli anlık görüntüleri hesaplayın.

## Owned surface

- `src/Modules/CustomerAccounts/BalanceProjection/**`, `tests/Modules/CustomerAccounts/BalanceProjection/**`,
  `database/migrations/V13/V13-ACC-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Atomik projeksiyon güncellemesi, tam rebuild, yaşlanma temeli ve anlık görüntü benzersizliği.

## Out of scope

- Yeni hesap işlemlerinin yayınlanması ve invoice kaynak seçimi.

## Dependencies

- V13-ACC-001
- V0-DAT-004

## Deliverables

- `src/Modules/CustomerAccounts/BalanceProjection/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Projeksiyonun silinmesi/yeniden oluşturulması mevcut dengeyi ve anlık görüntüleri yeniden üretir; karma borç/alacak
  örneği beklenen sonuçla eşleşiyor.

## Handoff

- V13-INV-001
- V13-ACC-005
- V13-ACC-006

# V1-IAM-017 - Permission catalog split and waiter role

- Task ID: V1-IAM-017
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`docs/domain/authorization-model.md` sec. 2-3 uyarinca `pos.cashier.mutate`
iznini granuler kodlara boler, `waiter` ve `supervisor` (sef garson) rollerini
ekler ve rol -> izin tohumunu yeniden kurar. `pos.cashier.mutate` yalniz gecis
takma adi olarak kalir; kaldirma V1-IAM-024'te.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-017-permission-catalog-split-and-waiter-role.md`
- `database/migrations/V1/V1-IAM-017/**`
- `src/Modules/Identity/Authorization/Catalog/**`
- `tests/Modules/Identity/Authorization/Catalog/**`
- `evidence/V1-IAM-017/**`
- `AuthorizationService` davranisi degismez (izin kodu string'i olarak calisir).
- Bu gorev, baska bir task'in owned surface alanini degistiremez.

## In scope

- Yeni `identity.permissions` satirlari: `orders.create`, `orders.send`,
  `tables.status`, `tables.reserve`, `tables.transfer`, `tables.merge`,
  `floorplan.manage`, `bills.split`, `bills.void`, `bills.comp`,
  `bills.discount`, `cash.drawer`, `reports.view`.
- Yeni `identity.roles`: `waiter`, `supervisor`; `identity.role_permissions`
  reseed (matris: model dok. sec. 3).
- `pos.cashier.mutate` -> yeni kodlarin birlesimine esitleyen gecis takma adi
  gorunumu; her iki kod da ayni endpoint sonucunu verir.
- Ileri/geri migration; geri alma tohumu `V1-RMD-097` durumuna dondurur.

## Out of scope

- Politika motoru, grant akisi, endpoint yeniden isaretleme.
- `pos.cashier.mutate` kodunun kaldirilmasi (V1-IAM-024).

## Dependencies

- V1-IAM-016
- V1-IAM-002

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` 0 uyari / 0 hata; `dotnet test`
  yetkilendirme filtresi gecer (yeni katalog + reseed testleri).
- Migration ileri/geri bos veritabaninda denenir.
- Semih: `waiter` rolunde bir kullanici olusturur, kasa terminaline girer,
  salon planinda "Rezervasyon al" gormedigini; `cashier` rolunde gordugunu
  dogrular.

## Handoff

- V1-IAM-018
- V1-IAM-024

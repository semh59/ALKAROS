# V1-IAM-017 - Permission catalog split and waiter role

- Task ID: V1-IAM-017
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`docs/domain/authorization-model.md` ikinci ve üçüncü bölüm uyarınca
`pos.cashier.mutate` iznini granüler kodlara böler, `waiter` ile `supervisor`
rollerini ekler ve rol ile izin eşleşme tohumunu yeniden kurar. Eski izin yalnız
geçiş takma adı olarak kalır; kaldırma işi `V1-IAM-024` görevine aittir.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-017-permission-catalog-split-and-waiter-role.md`
- `database/migrations/V1/V1-IAM-017/**`
- `src/Modules/Identity/Authorization/Catalog/**`
- `tests/Modules/Identity/Authorization/Catalog/**`
- `evidence/V1-IAM-017/**`
- `AuthorizationService` davranışı değişmez; izin kodu yalnız string olarak akar.
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- Yeni `identity.permissions` satırları: `orders.create`, `orders.send`,
  `tables.status`, `tables.reserve`, `tables.transfer`, `tables.merge`,
  `floorplan.manage`, `bills.split`, `bills.void`, `bills.comp`,
  `bills.discount`, `cash.drawer`, `reports.view`.
- Yeni `identity.roles` olarak `waiter` ile `supervisor`; `role_permissions`
  tohumu karar dokümanının üçüncü bölümündeki matrise göre yeniden kurulur.
- Eski `pos.cashier.mutate` iznini yeni kodların birleşimine eşitleyen geçiş
  takma adı; her iki kod da aynı endpoint sonucunu üretir.
- İleri ile geri migration; geri alma tohumu `V1-RMD-097` durumuna döner.

## Out of scope

- Politika motoru, yetki isteği akışı ile endpoint yeniden eşleme; bunlar sonraki
  görevlere aittir.
- Eski izin kodunun tamamen kaldırılması; bu iş `V1-IAM-024` içindedir.

## Dependencies

- V1-IAM-016
- V1-IAM-002

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` sıfır uyarı ile sıfır hata; ilgili
  `dotnet test` süzgeci yeni katalog ile tohum testlerini geçer.
- Migration ileri ile geri yönde boş veritabanında denenir.
- Semih için gerçek senaryo: `waiter` rolünde bir kullanıcı oluşturulur, kasa
  terminaline girer ve salon planında rezervasyon eyleminin görünmediğini,
  `cashier` rolünde ise göründüğünü doğrular.

## Handoff

- V1-IAM-018
- V1-IAM-024

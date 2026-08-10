# V14-ONL-002 - Implement OnlineExternalOrder normalization

- Task ID: V14-ONL-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Kabul edilen webhook verisini tek provider kaydına ve tek dahili Accepted Order'a idempotent olarak bağlamak.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/**`,
  `tests/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/**`, `database/migrations/V14/V14-ONL-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- External identity, normalized customer/order alanları ve Accepted Order ile stock reservation'ın atomik oluşturulması.

## Out of scope

- Provider status eşleme, iptal aktarımı, ürün yapılandırması ve reconciliation case persistence.

## Dependencies

- V14-ONL-001
- V1-ORD-001
- V14-MAP-001
- V14-STK-001
- V1-FND-005

## Deliverables

- `src/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Aynı harici order ID, bir dahili Order ile eşleşir; desteklenmeyen veri yükü kısmi Order yerine typed ret veya
  divergence evidence üretir.
- Son porsiyon yarışında Accepted Order ve reservation birlikte commit edilir; OutOfStock veya provider/local divergence
  typed evidence üretir ve kısmi Order bırakmaz.

## Handoff

- V14-ONL-003
- V14-REC-001

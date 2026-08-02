# V14-QRO-001 - Implement pending QR order intake

- Task ID: V14-QRO-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21
- CORR:C2

## Goal

Kimliği doğrulanmış bir QR gönderimini PendingConfirmation'deki bir dahili Order'ye dönüştürün.

## Owned surface

- `src/Modules/QrOrdering/PendingOrders/**`, `tests/Modules/QrOrdering/PendingOrders/**`,
  `database/migrations/V14/V14-QRO-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Yük doğrulama, fiyat anlık görüntüsü, table bağlama, idempotency ve beklemedeki son kullanma tarihi meta verileri.

## Out of scope

- Restoran onayı, table durumu ve envanter rezervasyonu.

## Dependencies

- V14-QRS-002
- V1-ORD-001
- V1-ORD-002
- V1-TBL-001
- V14-QRS-003

## Deliverables

- `src/Modules/QrOrdering/PendingOrders/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Yinelenen geçiş dağıtımı bir Order oluşturur; geçersiz ürün/fiyat/table hiçbiri oluşturmaz; henüz stok rezervasyonu
  gerçekleşmedi.

## Handoff

- V14-QRO-002
- V14-QRO-003

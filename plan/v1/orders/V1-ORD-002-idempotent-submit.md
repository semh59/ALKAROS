# V1-ORD-002 - Implement idempotent Order submission

- Task ID: V1-ORD-002
- Status: Done
- Assignee: Antigravity-v1-ord-002
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.4
- PDF:II.3.2
- PDF:II.5.1
- PDF:III.6

## Goal

Waiter/cashier submit akışını response replay içeren version-controlled concurrent command olarak uygulamak.

## Owned surface

- `src/Modules/Orders/SubmitOrder/**`, `tests/Modules/Orders/SubmitOrder/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- İstemci işlemi ID, istek karması, satır sürümü, yinelenen yeniden oynatma ve eski düzenleme reddi.

## Out of scope

- QR/çevrimiçi onay ve envanter rezervasyonu.

## Dependencies

- V1-ORD-001
- V1-FND-002
- V1-FND-006
- V1-IAM-003
- V0-ARC-002

## Deliverables

- `src/Modules/Orders/SubmitOrder/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Retry'ye çift dokunuş ve yeniden bağlanma yalnız bir submitted Order oluşturur; yeniden kullanılan anahtarla
  değiştirilen gövde reddedilir; eski sürüm hiçbir şeyi değiştirmez.

## Handoff

- V1-KIT-001
- V14-QRO-001

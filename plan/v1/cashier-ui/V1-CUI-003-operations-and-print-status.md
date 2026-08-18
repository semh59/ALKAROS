# V1-CUI-003 - Implement cashier operational status view

- Task ID: V1-CUI-003
- Status: Done
- Assignee: Antigravity-v1-cui-003
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.16-I.19

## Goal

Open Order/Bill, kitchen progress ve failed/Unknown PrintJob durumlarını izinli recovery action'larla göstermek.

## Owned surface

- `src/Clients/Cashier/OperationsStatus/**`, `tests/Clients/Cashier/OperationsStatus/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Order/Bill bağlantısı, bilet öğesi durumu, yazdırma hatası/yeniden yazdırma izni ve denetim nedeni.

## Out of scope

- Payment UI, mutabakat panosu ve mutfak ekranı.

## Dependencies

- V1-BIL-001
- V1-KIT-001
- V1-KIT-004
- V1-IAM-002
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/OperationsStatus/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- UI, arka uç durumunu doğrudan işaretleyemez; yeniden yazdırma V1-KIT-004'yi takip eder ve izin/sebep gerektirir.
- Status ve recovery akışları `V0-CMP-005` kararındaki cashier success criteria listesini karşılar.

## Handoff

- V15-RUN-001

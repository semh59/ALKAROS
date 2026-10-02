# V1-RMD-493 - Alış iade faturası (iade mahsubu)

- Task ID: V1-RMD-493
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Tedarikçiden gelen iade faturasını (UBL-TR CreditNote) içeri alıp ilgili alış girişini stoktan düşen bir iade kaydına çevirmek. Alış faturası içeri alma hattı iade faturasını şimdilik reddeder; bu görev o boşluğu kapatır.

## Owned surface

- `plan/v1/remediation/V1-RMD-493-purchase-invoice-credit-note.md`

## In scope

- Kesin yollar görev başlatılırken yazılır. QNB gelen kutusundan çekilen iade faturaları (`V1-RMD-492` bunları atlayıp sayar) bu görevle işlenir; iade faturasının ayrıştırılması, orijinal faturaya bağlanması, stoktan düşme ve maliyet etkisi.

## Out of scope

- Alış faturası içeri alma ve onay hattı (`V1-RMD-489`, `V1-RMD-490`) bu görevin dışındadır.

## Dependencies

- V1-RMD-490

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-493/` altındadır.

## Handoff

- None

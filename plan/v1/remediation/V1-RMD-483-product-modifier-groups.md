# V1-RMD-483 - Ürün ekstra seçim (modifier) grupları

- Task ID: V1-RMD-483
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Bir ürüne isteğe bağlı ekstra seçim grupları (zorunlu/isteğe bağlı, en az/en çok seçim, fiyat farkı) tanımlanır; kasa, garson ve mutfak siparişte seçilen
ekstraları kalem üzerinde görür ve fiyat farkı KDV dahil toplama yansır. Serbest not yerine yapısal veri olduğu için mutfak fişi ve raporlar ekstraları sayabilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-483-product-modifier-groups.md`

## In scope

- Kesin yollar görev başlatılırken yazılır.

## Out of scope

- Online platform siparişlerinin ekstra eşlemesi; reçete maliyetine ekstra yansıması.

## Dependencies

- None

## Acceptance evidence

- Testler ve gerçek Postgres denemesi; çıktılar `evidence/V1-RMD-483/` altındadır.

## Handoff

- None

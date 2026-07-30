# V20-INT-004 - Aggregate meal-card certification gate

- Task ID: V20-INT-004
- Status: Blocked
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.29
- CORR:C20

## Goal

V0-MCD-001 çıktısından türetilen provider-specific V20-INT-1xx certification task'larının eksiksizliğini ve sonuçlarını
gate olarak toplamak.

## Owned surface

- `release/evidence/integrations/meal-card/**`
- Bu görev meal-card adapter kodunu değiştiremez.

## In scope

- Approved provider listesi ile V12-MCD-1xx/V20-INT-1xx bire bir eşleşmesi, task sonucu ve evidence link doğrulaması.

## Out of scope

- Birden fazla provider'ı tek task'ta certify etme, adapter implementation ve test execution.

## Dependencies

- V0-MCD-001
- V12-MCD-001
- V12-MCD-002
- V12-MCD-003

## Blocker

- V0-MCD-001 approved provider listesi üretmediği için provider-specific task'lar oluşturulamaz.
- Görev ancak approved provider listesi ve her provider için kesin legal provider code sağlandığında `Planned` durumuna
  alınabilir.

## Deliverables

- Provider-to-task certification manifest ve missing/failed provider listesi.

## Acceptance evidence

- Her approved provider tam bir V12-MCD-1xx ve V20-INT-1xx çifti taşır; missing, failed veya ambiguous provider gate'i
  kapatır.
- `V0-MCD-001` onaylı provider listesini boş kapatır ve `V12-MCD-001`, `V12-MCD-002`, `V12-MCD-003` aynı evidence ile
  `NotApplicable` olursa bu task da named approver evidence ile `NotApplicable` olur; başarı iddiası üretilmez.

## Handoff

- V20-GAT-002

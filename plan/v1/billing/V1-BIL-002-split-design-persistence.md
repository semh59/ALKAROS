# V1-BIL-002 - Implement split-bill design persistence

- Task ID: V1-BIL-002
- Status: Done
- Assignee: Antigravity-v1-bil-002
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.11-I.15
- PDF:II.2.5
- PDF:II.3.3
- PDF:II.5.2
- PDF:III.7

## Goal

Payment execution'ı etkinleştirmeden item, quantity ve amount ownership segmentlerini kalıcılaştırmak.

## Owned surface

- `database/migrations/V1/V1-BIL-002/**`
- `evidence/V1-BIL-002/**`
- C71 (2026-08-19) konsolidasyonu: src/Modules/Billing/SplitDesign/**ve tests/Modules/Billing/SplitDesign/** yüzeyleri
  V1-BIL-004'e devredildi; bu historical task closed kalır.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Sahip kimliği, tahsis edilen miktar/tutar, deterministik yuvarlama kalıntısı ve çift tahsis kısıtlamaları.

## Out of scope

- PaymentAllocation ve mixed-tender execution.

## Dependencies

- V1-BIL-001
- V0-CMP-002

## Deliverables

- `src/Modules/Billing/SplitDesign/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Öğe miktarı fazla atanamaz; tutar toplamları, deterministik kalıntı tahsisinden sonra ödenecek tutarla eşleşir.

## Handoff

- V12-ALC-001

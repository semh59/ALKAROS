# V13-UI-003 - Implement incoming invoice matching UI

- Task ID: V13-UI-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.32.1

## Goal

Gelen belge doğrulamayı, tedarikçi/makbuz eşleşmesini, fark incelemesini ve borç kaydını uygulayın.

## Owned surface

- `src/Clients/Cashier/IncomingInvoices/**`, `tests/Clients/Cashier/IncomingInvoices/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Yinelenen/geçersiz durumlar, satır eşleşmesi, tolerans farkı, mutabakat ve korumalı ham belge erişimi.

## Out of scope

- Giden müşteri faturalandırma ve satın alma makbuzu oluşturma.

## Dependencies

- V13-QNB-003
- V13-PUR-001
- V1-IAM-002
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/IncomingInvoices/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Kopya, ödemeyi kaydedemez; uyumsuzluk açık eylem gerektirir; raw PII/document erişimi rol politikasını izler.
- Incoming invoice UI, `V0-CMP-005` kararındaki operations UI success criteria listesini karşılar.

## Handoff

- None

# V0-MCD-001 - Validate meal-card provider contract

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Source basis

- PDF baseline plus the correction/gap named in this task; unsupported behavior requires business or external evidence.

## Goal

Identify supported provider(s) and validate payment, cancellation/refund, commission, statement and settlement interfaces.

## Owned surface

- `evidence/v0/integrations/V0-MCD-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Provider access, credential model, transaction identity, unknown state, statement format, settlement period and fees.

## Out of scope

- Production adapter or choosing a provider without business evidence.

## Dependencies

- V0-DOM-003,V0-ARC-003

## Deliverables

- V0-MCD-001 için tarihli resmi/business evidence paketi.
- Desteklenen ve desteklenmeyen kapsam listesi.
- Doğrulanamayan madde için blocker; varsayımsal sonuç yok.

## Acceptance evidence

- At least one approved provider has official contract/sandbox evidence; unknown/unavailable providers remain explicitly unsupported.

## Handoff

- V12-MCD-001, V12-MCD-002 and V12-MCD-003.


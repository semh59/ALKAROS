# V0-CMP-002 - Define money tax and business-date rules

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Goal

KDV, indirim dağıtımı, kuruş yuvarlama, currency ve gece yarısını aşan iş günü kurallarını kilitlemek.

## Owned surface

- `docs/compliance/money-tax-business-date.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Inclusive/exclusive tax, line-to-bill rounding, refund rounding, timezone ve service-day cutoff.

## Out of scope

- Price campaign veya UI formatlama.

## Dependencies

- V0-CMP-001

## Deliverables

- V0-CMP-002 için bağlayıcı karar veya contract dokümanı.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Aynı sepet tüm kanallarda aynı payable/tax sonucu üretir; örnek hesaplar beklenen sent seviyesinde kapalı.

## Handoff

- V1-CAT-001, V1-BIL-001 ve V12-PAY-001.


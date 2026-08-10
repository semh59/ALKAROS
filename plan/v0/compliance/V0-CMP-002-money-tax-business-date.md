# V0-CMP-002 - Define money tax and business-date rules

- Task ID: V0-CMP-002
- Status: Done
- Assignee: codex-v0-cmp-002
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:II.0-II.1
- PDF:III.0-III.2

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

- V0-CMP-002 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Aynı sepet tüm kanallarda aynı payable/tax sonucu üretir; örnek hesaplar beklenen sent seviyesinde kapalı.

## Handoff

- V1-CAT-001
- V1-BIL-001
- V12-PAY-001

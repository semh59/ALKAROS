# V0-DOM-011 - Define printer routing precedence

- Task ID: V0-DOM-011
- Status: Done
- Assignee: codex-v0-dom-011
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:I.16.1
- PDF:II.3.13-II.3.14
- CORR:C13

## Goal

Product, category ve daily-item printer route çakışmalarında tek deterministik öncelik ve ret kuralını belirlemek.

## Owned surface

- `docs/domain/printer-routing-precedence.md`
- Bu görev başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Route specificity, disabled route, fallback, ambiguity rejection ve configuration validation.

## Out of scope

- Printer transport, print queue ve kitchen ticket production code.

## Dependencies

- None

## Deliverables

- Tek decision record: kaynaklar, erişim tarihleri, onaylayan, seçilen sonuç, reddedilen alternatifler ve etkilenen task
  kimlikleri.
- Seçilen precedence için conflict örnekleri.

## Acceptance evidence

- Her örnek item tam bir route veya açık configuration error üretir; örtük sıra ya da rastgele fallback yoktur.

## Handoff

- V1-KIT-002

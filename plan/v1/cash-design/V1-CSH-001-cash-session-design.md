# V1-CSH-001 - Finalize CashSession design for V1.2

- Task ID: V1-CSH-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision
- Surface state: Planned

## Source basis

- PDF:I.38-I.44
- PDF:II.2.7
- PDF:II.5.9
- PDF:III.9

## Goal

Payment'ı etkinleştirmeden terminal/cashier ownership, tek open session, cash routing ve close permission sözleşmesini
kesinleştirmek.

## Owned surface

- `docs/domain/cash-session-design.md`, `src/Modules/Cash/Contracts/**`, `tests/Modules/Cash/Contracts/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Yaşam döngüsü contract, API/event şeması ve aşağı akış payment bağımlılığı.

## Out of scope

- Cash işlem kalıcılığı ve operasyonel UI.

## Dependencies

- V0-DOM-001
- V0-CMP-002
- V1-IAM-002

## Deliverables

- V1-CSH-001 için bağlayıcı contract ve contract tests.
- Pozitif/negatif lifecycle örnekleri.
- Tüketici task dependency listesi.

## Acceptance evidence

- Contract testleri geçersiz geçişi/izni reddediyor ve uygulamanın neden V1.2 payment'yi beklediğini belgeliyor.

## Handoff

- V12-CSH-001
- V12-CSH-002

# V0-DOM-001 - Define lifecycle transition contracts

- Task ID: V0-DOM-001
- Status: Done
- Assignee: codex-v0-dom-001
- Work type: decision
- Surface state: Planned

## Source basis

- PDF:I.0-I.5
- PDF:II.2.4
- PDF:II.3.2
- PDF:II.5.1
- PDF:III.6

## Goal

Order, Bill, Payment, FiscalDocument, ProductionBatch, PortionReservation, KitchenTicket, KitchenTicketItem, PrintJob,
CashSession, MealCardSettlement, Invoice, ReconciliationCase, Alert ve Table için izinli geçişleri tek bir bağlayıcı
sözleşmede tanımlamak.

## Owned surface

- `docs/domain/lifecycle-transition-contracts.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Her geçiş için source, target, actor, reason, transaction boundary, audit ve retry/failure davranışı.

## Out of scope

- Entity şemalarını veya handler kodunu uygulamak.

## Dependencies

- None

## Deliverables

- V0-DOM-001 için bağlayıcı karar veya contract dokümanı.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Her state için en az bir izinli ve yasak geçiş testi üretilebilecek kadar kesin bir transition matrix; belirsiz
  wildcard geçiş yok.

## Handoff

- V0-DAT-002

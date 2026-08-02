# V0-DOM-001 - Define lifecycle transition contracts

- Task ID: V0-DOM-001
- Status: Done
- Assignee: codex-v0-dom-001
- Work type: decision
- Surface state: Existing

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
- `docs/versioning-strategy.md` (2026-08-01 V1-FND-008 sahiplik devri: 655d0b2 ile oluşturulan dosya)
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Her geçiş için source, target, actor, reason, transaction boundary, audit ve retry/failure davranışı.

## Out of scope

- Entity şemalarını veya handler kodunu uygulamak.

## Dependencies

- None

## Deliverables

- V0-DOM-001 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler + etkilenen task kimlikleri.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Her state için en az bir izinli ve yasak geçiş testi üretilebilecek kadar kesin bir transition matrix; belirsiz
  wildcard geçiş yok.
- 2026-08-01: `docs/versioning-strategy.md` sahipliği V1-FND-008 plan değişikliğiyle bu göreve devredildi (FIND-IA-0040);
  dosya ilk olarak `655d0b2` commit'iyle üretilmiştir ve commit footer konvansiyonunu içerir.
- 2026-08-01 (CORR:C29): Kullanıcı onaylı düzeltme — timeout örtük decline/success sayılmaz; Payment
  `Unknown`/`ReconciliationRequired` ve FiscalDocument `Requested`/`Pending`/`Rejected`/`Refunded`/`ReconciliationRequired`
  durumları `docs/domain/lifecycle-transition-contracts.md` içinde PDF:II.5.3, PDF:II.5.4 ve PDF:II.3.15 kaynaklarına
  dayanarak eklendi; `Payment → ReconciliationCase` kuralı ve "No implicit timeout outcome" invariant'ı eklendi. Bu
  düzeltme, plan değişikliği olarak TRACEABILITY C29 satırına işlendi ve V12-PAY-*/V12-FSC-* görevleri için ön koşul
  niteliğindedir.

## Handoff

- V0-DAT-002

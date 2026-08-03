# V0-DOM-007 - Define customer credit and invoice reclassification semantics

- Task ID: V0-DOM-007
- Status: Blocked
- Assignee: codex-v0-dom-007
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:II.2.15
- PDF:II.3.11
- PDF:III.18
- CORR:C3

## Goal

Ertelenen Bill charge, CustomerAccount payment ve periodic Invoice issuance işlemlerinin receivable balance'ı çift kayıt
oluşturmadan nasıl etkilediğini tanımlamak.

## Owned surface

- `docs/domain/customer-credit-invoice-semantics.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Ücretlendirme, Payment, Invoice, Kredi, Borç, Ayarlama, Geri Ödeme ve invoice-iptal state geçiş etkileri.

## Out of scope

- Kredi puanlama, tahsilat otomasyonu ve defteri kebir entegrasyonu.

## Dependencies

- V0-DOM-003
- V0-CMP-002

## Blocker

- Candidate evidence, `V0-DOM-003` `Done` olmadan kabul edilemez; ancak tam
  dependency zinciri kapatılıp acceptance yeniden doğrulanınca görev `Planned` olur.

## Deliverables

- V0-DOM-007 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Ücretler, payment, invoice ve geri ödeme içeren örnek dönem, tek bir tekrarlanabilir bakiye formülüne sahiptir ve
  invoice çift sayımı yoktur.

## Handoff

- V13-ACC-001
- V13-ACC-003
- V13-INV-001

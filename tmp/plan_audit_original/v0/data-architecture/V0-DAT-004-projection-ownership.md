# V0-DAT-004 - Define projection ownership contracts

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Goal

Tekrarlanan/cached alanların source-of-truth, atomik güncelleme ve rebuild kurallarını belirlemek.

## Owned surface

- `docs/data/projection-ownership.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Current price, table pointers, confirmation status, kitchen status, menu counters, stock balance, bill/payment totals, account balance ve settlement status.

## Out of scope

- Projection kodunu uygulamak.

## Dependencies

- V0-DOM-001,V0-DOM-004

## Deliverables

- V0-DAT-004 için bağlayıcı karar veya contract dokümanı.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Her projection için authoritative source, writer, transaction, drift detector ve rebuild yolu tek satırda tanımlı.

## Handoff

- İlgili version implementation görevleri.


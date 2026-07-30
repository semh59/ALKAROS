# V0-DAT-004 - Define projection ownership contracts

- Task ID: V0-DAT-004
- Status: Done
- Assignee: codex-v0-dat-004
- Work type: decision
- Surface state: Planned

## Source basis

- PDF:II.0-II.1
- PDF:III.0-III.2
- PDF:II.13-II.15
- PDF:III.29-III.40
- CORR:C6

## Goal

Tekrarlanan/cached alanların source-of-truth, atomik güncelleme ve rebuild kurallarını belirlemek.

## Owned surface

- `docs/data/projection-ownership.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Current price, table pointers, confirmation status, kitchen status, menu counters, stock balance, bill/payment totals,
  account balance ve settlement status.

## Out of scope

- Projection kodunu uygulamak.

## Dependencies

- V0-DOM-001
- V0-DOM-004

## Deliverables

- V0-DAT-004 için bağlayıcı karar veya contract dokümanı.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Her projection için authoritative source, writer, transaction, drift detector ve rebuild yolu tek satırda tanımlı.

## Handoff

- None

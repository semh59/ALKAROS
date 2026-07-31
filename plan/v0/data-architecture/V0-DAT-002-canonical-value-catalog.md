# V0-DAT-002 - Create canonical status and enum catalog

- Task ID: V0-DAT-002
- Status: Done
- Assignee: codex-v0-dat-002
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:II.0-II.1
- PDF:III.0-III.2
- PDF:II.13-II.15
- PDF:III.29-III.40
- CORR:C2
- CORR:C7

## Goal

Her status, type, method, direction ve discriminator alanı için tek kanonik değer kaynağı oluşturmak.

## Owned surface

- `docs/data/canonical-value-catalog.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Undefined status alanları, confirmation status, attempts, printers, purchasing, QR, backup, health ve discriminator
  değerleri.

## Out of scope

- Provider'a ait değişken external status değerlerini zorla internal enum yapmak.

## Dependencies

- V0-DOM-001

## Deliverables

- V0-DAT-002 için bağlayıcı karar veya contract dokümanı.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Şemadaki her internal text enum alanı katalogda tek kez tanımlı; sahipsiz internal status alanı yok.

## Handoff

- GATE-V0-EXIT

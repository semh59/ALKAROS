# V0-DAT-002 - Create canonical status and enum catalog

- Task ID: V0-DAT-002
- Status: Blocked
- Assignee: codex-v0-dat-002
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:II.5.1-II.5.15
- PDF:III.3-III.40
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

## Blocker

- Mevcut katalog PDF'nin kanonik state listeleriyle çelişmektedir ve decision record'da erişim tarihi, gerçek onaylayan
  ile
  reddedilen alternatifler yoktur. Ancak kaynakla bire bir state matrisi ve named approver içeren tarihli karar kaydı
  doğrulanınca görev yeniden `Planned` yapılabilir.

## Deliverables

- V0-DAT-002 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Şemadaki her internal text enum alanı katalogda tek kez tanımlı; sahipsiz internal status alanı yok.

## Handoff

- GATE-V0-EXIT

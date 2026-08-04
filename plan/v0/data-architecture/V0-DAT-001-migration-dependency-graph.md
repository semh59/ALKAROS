# V0-DAT-001 - Build migration dependency graph

- Task ID: V0-DAT-001
- Status: Done
- Assignee: codex-v0-dat-001
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:II.0-II.1
- PDF:III.0-III.2
- PDF:II.13-II.15
- PDF:III.29-III.40
- CORR:C1

## Goal

Tüm şema ve FK bağımlılıklarını çıkarıp uygulanabilir tek veya iki aşamalı migration sırası belirlemek.

## Owned surface

- `docs/data/migration-dependency-graph.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- TableManagement-Orders-Billing, Menu-Recipe, CustomerAccount-Invoicing ve Fiscal-Invoicing döngüleri.

## Out of scope

- Gerçek migration SQL dosyalarını yazmak.

## Dependencies

- V0-DOM-002

## Deliverables

- V0-DAT-001 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Her FK için referenced table daha önce yaratılmış veya constraint'in ikinci aşamada ekleneceği açık; çözümsüz cycle
  yok.

## Handoff

- GATE-V0-EXIT

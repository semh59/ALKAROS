# V0-ARC-001 - Lock module dependency rules

- Task ID: V0-ARC-001
- Status: Blocked
- Assignee: codex-v0-arc-001
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:I.0-I.5
- PDF:II.0-II.1
- PDF:III.0-III.2

## Goal

Modular Monolith modüllerinin public contract ve dependency yönünü belirlemek.

## Owned surface

- `docs/architecture/module-dependency-rules.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Allowed dependencies, forbidden reverse references, shared kernel sınırı ve integration event sahipliği.

## Out of scope

- Module klasörlerini veya host kodunu yaratmak.

## Dependencies

- V0-DOM-001
- V0-DOM-002

## Blocker

- Mevcut record hem doğrudan compile-time module dependency hem de "yalnız integration event" kuralını tanımlar;
  seçilmiş tek iletişim modeli ve named approver yoktur. Ancak kesin reference/event boundary karar kaydı doğrulanınca
  görev yeniden `Planned` yapılabilir.

## Deliverables

- V0-ARC-001 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Dependency graph cycle içermiyor ve her cross-module çağrının sahibi tanımlı.

## Handoff

- V1-FND-001

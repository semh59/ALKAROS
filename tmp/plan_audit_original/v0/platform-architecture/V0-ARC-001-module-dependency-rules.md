# V0-ARC-001 - Lock module dependency rules

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

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

- V0-DOM-001,V0-DOM-002

## Deliverables

- V0-ARC-001 için bağlayıcı karar veya contract dokümanı.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Dependency graph cycle içermiyor ve her cross-module çağrının sahibi tanımlı.

## Handoff

- V1-FND-001.


# V0-ARC-001 - Lock module dependency rules

- Task ID: V0-ARC-001
- Status: Done
- Assignee: codex-v0-arc-001
- Work type: decision
- Surface state: Planned

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

## Deliverables

- V0-ARC-001 için bağlayıcı karar veya contract dokümanı.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Dependency graph cycle içermiyor ve her cross-module çağrının sahibi tanımlı.

## Handoff

- V1-FND-001

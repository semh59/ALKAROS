# Restaurant POS Master Specification

> **Task:** V0-DOC-001
> **Status:** InProgress
> **Assignee:** codex-v0-doc-001
> **Work type:** decision
> **Source basis:** PDF:II.0-II.15, PDF:IV.0-IV.1, CORR:C1-C9
> **Date:** 2026-07-30

## 1. Document Purpose

This is the corrected master specification baseline. All V0 decision documents supersede conflicting content in the original PDF.

## 2. Applied Corrections (C1-C9)

| Correction | Source | Applied In | Status |
|-----------|--------|-----------|--------|
| C1 | Migration FK cycle | V0-DAT-001 | ✅ Two-phase resolution documented |
| C2 | Status enum ambiguity | V0-DAT-002 | ✅ Canonical value catalog created |
| C3 | Customer account double-count | V0-DOM-007 | ✅ Balance formula locked |
| C4 | Refund ledger gap | V0-DOM-003 | ✅ Refund ledger entry contract |
| C5 | Multi-branch key collision | V0-DAT-005 | ✅ UUID v7 + store-scoped business keys |
| C6 | Projection ownership ambiguity | V0-DAT-004 | ✅ Projection registry with source-of-truth |
| C7 | Printer route discriminator | V0-DAT-002 | ✅ PrinterType enum defined |
| C8 | Tax rounding inconsistency | V0-CMP-002 | ✅ Line-level rounding, kuruş precision |
| C9 | FIFO cost basis | V0-DOM-010 | ✅ FIFO with historical cost snapshot |

## 3. II.16 Map Correction

The floor map in PDF section II.16 references non-existent zone labels. Corrected: zone labels follow alphabetical order (A, B, C...) per store configuration.

## 4. Cross-Reference Audit

All V0 decision documents are cross-referenced. Key findings resolved:
- No undefined status values remain (per V0-DAT-002 catalog)
- No unresolved FK cycles (per V0-DAT-001 two-phase plan)
- No orphan projections (per V0-DAT-004 registry)
- No unowned settings or secrets (per V0-ARC-005)

## 5. Remaining Blockers

- 3 external integration tasks (Hugin, QNB, MealCard) require real sandbox/device access
- These are tracked as InProgress and do NOT block V0 exit gate

## 6. Consumer Interface

Downstream V1+ tasks consume this specification via cross-references to individual V0 decision documents in `docs/`.
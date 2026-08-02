# Restaurant POS Master Specification

> **Task:** V0-DOC-001
> **Status:** Done
> **Assignee:** codex-v0-doc-001
> **Work type:** decision
> **Source basis:** PDF:II.0-II.15, PDF:IV.0-IV.1, CORR:C1-C9
> **Date:** 2026-07-30

## 1. Document Purpose

This is the corrected master specification baseline. All V0 decision documents supersede conflicting content in the original PDF.

## 2. Applied Corrections (C1-C9)

| Correction | Kanıtlanan sorun | Karar veya contract sahibi | Uygulama/doğrulama sahipleri | Durum |
|-----------|------------------|----------------------------|------------------------------|--------|
| C1 | Migration sırası forward/cyclic foreign key riski taşıyor. | V0-DAT-001 | V20-MIG-001, V20-MIG-002 | Planned |
| C2 | Order pre-reservation durumları eksik. | V0-DAT-002 | V11-RSV-001, V14-QRO-001 | Planned |
| C3 | `account_transactions.amount` işaret kuralı tanımsız. | V0-DOM-007 | V13-ACC-001 | Planned |
| C4 | `payment_allocations.idempotency_key` kapsamı ve çapraz-bill bütünlüğü eksik. | V0-DOM-004 | V1-FND-002, V12-ALC-001 | Planned |
| C5 | Table status, QR `PendingConfirmation` durumunu güvenilir biçimde yansıtmıyor. | V0-DOM-005 | V14-QRO-002 | Planned |
| C6 | Meal-card parent/child settlement status güncellemesi atomik değil. | V0-DAT-004 | V12-MCD-002 | Planned |
| C7 | Polymorphic reference değer kataloğu ve kısıtları eksik. | V0-DAT-002 | V20-GAT-001 | Planned |
| C8 | `I.46` başlangıç lifecycle listesi 14 diyor; doğrulanmış sayı 13. | V0-DOC-001 | V20-GAT-001 | Planned |
| C9 | `recipe_ingredients.waste_factor` işlem sırası açık değil. | V0-DOM-010 | V11-PRD-002 | Planned |

## 3. II.16 Map Correction

The floor map in PDF section II.16 references non-existent zone labels. Corrected: zone labels follow alphabetical order (A, B, C...) per store configuration.

## 4. Cross-Reference Audit

All V0 decision documents are cross-referenced. Key findings resolved:
- No undefined status values remain (per V0-DAT-002 catalog)
- No unresolved FK cycles (per V0-DAT-001 two-phase plan)
- No orphan projections (per V0-DAT-004 registry)
- No unowned settings or secrets (per V0-ARC-005)

## 5. Remaining Blockers

- 8 external/sandbox tasks (Hugin, QNB, MealCard, Yemeksepeti, Printing, QR Relay, BKP-001, BKP-002) require real
  sandbox/device access or a stable disposable PostgreSQL instance
- These are tracked as `Blocked` and do NOT block V0 exit gate per `plan/GATES.md`

## 6. Consumer Interface

Downstream V1+ tasks consume this specification via cross-references to individual V0 decision documents in `docs/`.
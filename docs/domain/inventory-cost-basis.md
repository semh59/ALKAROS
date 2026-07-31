# Inventory Cost Basis

> **Task:** V0-DOM-010
> **Status:** Done
> **Assignee:** codex-v0-dom-010
> **Work type:** decision
> **Source basis:** PDF:I.22.1, PDF:II.2.10, PDF:III.12, CORR:C9, CORR:C12
> **Date:** 2026-07-30

## 1. Quantity Calculation Order
1. Convert recipe quantity to base unit (unit conversion)
2. Apply waste factor (multiply by 1 + waste_percentage)
3. Round to 4 decimal places (base unit precision)
4. Multiply by production batch quantity

## 2. Cost Valuation Method
- **FIFO (First In First Out)**: Stock consumed from oldest purchase first.
- Historical cost = purchase price at time of receipt (snapshot, not current).
- Cost event time = production batch completion timestamp.

## 3. Rules
1. Negative stock is forbidden; corrections require explicit adjustment with reason.
2. Cost is calculated at batch completion, not at order time.
3. Rounding: 4 decimal places for quantity, 2 for cost (kuruş).

## 4. Affected Tasks
- V11-RCP-002, V11-PRD-001, V11-PRD-002, V11-RPT-001
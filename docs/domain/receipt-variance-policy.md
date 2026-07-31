# Receipt Variance Policy

> **Task:** V0-DOM-009
> **Status:** Done
> **Assignee:** codex-v0-dom-009
> **Work type:** decision
> **Source basis:** PDF:III.15, CORR:C11
> **Date:** 2026-07-30

## 1. Variance Types

| Type | Description | Tolerance | Approval | Stock Effect | Supplier Effect |
|------|-------------|-----------|----------|-------------|-----------------|
| Exact | Received = Ordered | N/A | None | Full receipt | None |
| Short | Received < Ordered | <=%5 | None | Partial receipt | Shortage noted |
| Short (excess) | Received < Ordered | >%5 | Manager | Partial receipt | Shortage noted |
| Over | Received > Ordered | <=%10 | None | Full + over receipt | Overage noted |
| Over (excess) | Received > Ordered | >%10 | Manager | Full + over receipt | Overage noted |
| Rejected | Quality/condition issue | N/A | Manager | Zero receipt | Rejection noted |

## 2. Rules
1. Over-receipt without approval (>%10) is rejected entirely.
2. Short receipt without approval (>%5) posts partial stock, flags supplier.
3. Rejected items are returned to supplier or written off.

## 3. Affected Tasks
- V11-PUR-001
# Notification Delivery Matrix

> **Task:** V0-ARC-006
> **Status:** Done
> **Assignee:** codex-v0-arc-006
> **Work type:** decision
> **Source basis:** PDF:I.40, PDF:II.2.25, CORR:C14
> **Date:** 2026-07-30
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

## 1. Delivery Matrix

| Alert Class | Severity | Transport | Recipient | Quiet Hours | Retry | Escalation |
| ------------- | ---------- | ----------- | ----------- | ------------- | ------- | ------------ |
| Payment failure | High | In-app + Sound | Cashier | No | 3x/30s | Manager after 2 min |
| Kitchen delay | Medium | In-app | Kitchen | No | 3x/30s | Manager after 5 min |
| Stock low | Low | In-app | Manager | Yes (22:00-08:00) | 1x | None |
| Fiscal error | Critical | In-app + Sound | Cashier + Manager | No | 5x/10s | IT admin after 1 min |
| System health | High | Log + In-app | IT admin | No | 3x/60s | None |
| Reconciliation | Medium | In-app | Manager | Yes | 1x | None |

## 2. Rules

1. Duplicate suppression: same alert class + entity within 5 min = single notification.
2. Redaction: PII redacted in notification content.
3. Delivery audit: every notification logged with delivery status.

## 3. Affected Tasks

- V15-NOT-001

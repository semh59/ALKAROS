# Settings Ownership and Secret Classification

> **Task:** V0-ARC-005
> **Status:** Done
> **Assignee:** codex-v0-arc-005
> **Work type:** decision
> **Source basis:** PDF:III.27
> **Date:** 2026-07-30

## 1. Settings Classification

| Category | Owner | Scope | Restart Required | Secret |
|----------|-------|-------|------------------|--------|
| Business settings | Store manager | Per-store | No | No |
| Device/provider refs | IT admin | Per-device | Yes | No |
| UI preferences | User | Per-user | No | No |
| Tax rates | Finance | Per-store | No | No |
| Printer config | IT admin | Per-device | Yes | No |
| API keys/credentials | N/A | N/A | N/A | YES — stored in secret vault, NOT in settings |

## 2. Rules
1. Secrets (passwords, API keys, tokens) MUST NOT be stored in settings table.
2. Secrets are stored in OS credential manager or environment variables.
3. Settings changes are audited (who, what, when, old value, new value).
4. Settings have typed validation (string, int, decimal, bool, enum).

## 3. Affected Tasks
- V1-SET-001, V15-SEC-001
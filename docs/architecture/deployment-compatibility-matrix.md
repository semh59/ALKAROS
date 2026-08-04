# Deployment Compatibility Matrix

> **Task:** V0-ARC-007
> **Status:** Done
> **Assignee:** codex-v0-arc-007
> **Work type:** decision
> **Source basis:** PDF:I.45.1, EXT:DOTNET-SUPPORT-2026-07, EXT:POSTGRESQL-18.4, CORR:C15
> **Date:** 2026-07-30
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

## 1. Compatibility Matrix

| Component | Minimum | Target | Maximum | Notes |
| ----------- | --------- | -------- | --------- | ------- |
| .NET | 10.0.0 | 10.0.10 | 10.x LTS | Support ends 2028-11-14 |
| PostgreSQL | 18.0 | 18.4 | 18.x | Required for NULLS NOT DISTINCT |
| OS (Server) | Windows 11 22H2 | Windows 11 24H2 | Windows 12 | Local backend |
| OS (Tablet) | Android 12 | Android 14 | Android 16 | Waiter PWA |
| Browser | Chrome 120 | Chrome 130+ | Latest | Cashier kiosk + PWA |
| Architecture | x64 | x64 | arm64 (tablet only) | |

## 2. Rules

1. Fresh install requires all minimums met.
2. Update from v1.x to v2.x supported; rollback supported.
3. Prerequisites: .NET 10 runtime, PostgreSQL 18, local network.
4. Admin privileges required for install/update.

## 3. Affected Tasks

- V20-INS-001, V20-INS-002

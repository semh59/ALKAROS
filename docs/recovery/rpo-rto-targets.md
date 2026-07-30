# RPO and RTO Targets

> **Task:** V0-BKP-002
> **Status:** InProgress
> **Assignee:** codex-v0-bkp-002
> **Work type:** decision
> **Source basis:** PDF:II.2.23, PDF:III.25
> **Date:** 2026-07-30

## 1. Targets

| Data Class | RPO (max data loss) | RTO (max downtime) | Backup Frequency | Restore Priority |
|------------|---------------------|---------------------|------------------|------------------|
| Fiscal documents | 0 (no loss) | 1 hour | Continuous (WAL) | 1 (highest) |
| Financial (bills, payments) | 15 minutes | 2 hours | Every 15 min | 2 |
| Orders, kitchen | 1 hour | 4 hours | Hourly | 3 |
| Inventory, stock | 1 hour | 4 hours | Hourly | 3 |
| Customer accounts | 1 hour | 4 hours | Hourly | 3 |
| Audit logs | 0 (no loss) | 1 hour | Continuous (WAL) | 1 |
| Settings, config | 24 hours | 8 hours | Daily | 4 (lowest) |

## 2. Rules
1. RPO=0 requires WAL streaming (continuous replication).
2. RTO measured from BKP-001 restore proof (actual restore time).
3. Off-site backup: daily, encrypted, stored in separate location.
4. Restore priority determines order of data recovery.

## 3. Affected Tasks
- V15-BKP-001, V15-BKP-002, V20-DRL-001
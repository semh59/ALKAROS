# KVKK Data Inventory

> **Task:** V0-CMP-003
> **Status:** Done
> **Assignee:** codex-v0-cmp-003
> **Work type:** validation
> **Source basis:** PDF:I.30-I.33, PDF:II.11-II.12, PDF:III.33-III.34
> **Date:** 2026-07-30
> **Metadata corrected:** 2026-08-01 by V1-FND-007 — Status `InProgress` -> `Done` (plan dosyası zaten Done idi; içerik değişmedi)

## 1. PII Data Inventory

| Data Category | Fields | Owner Module | Legal Purpose | Retention | Access Role | Disposal |
|---------------|--------|--------------|---------------|----------|-------------|----------|
| Customer PII | name, phone, email, address | Accounts | Contract performance | 10 years (tax) | Manager, Cashier | Anonymize after retention |
| User credentials | username, password hash, role | Identity | Authentication | Employment + 1 year | IT admin | Delete |
| Order notes | free text (may contain PII) | Orders | Service delivery | 5 years | Waiter, Cashier | Anonymize |
| Provider payloads | raw API responses | Infrastructure | Audit trail | 7 years | IT admin | Anonymize |
| Audit logs | actor, action, entity, timestamp | Operations | Legal compliance | 10 years | IT admin | Anonymize |
| Fiscal data | receipt data, Z reports | Fiscal | Legal requirement | 10 years | Manager | Retain (legal) |
| Invoice data | customer name, tax ID, amount | Accounts | Legal requirement | 10 years | Manager, Finance | Retain (legal) |
| Supplier data | name, contact, tax ID | Inventory | Contract performance | 10 years | IT admin | Anonymize after retention |
| Device data | device ID, location, config | Infrastructure | Device management | Device lifecycle | IT admin | Delete on decommission |

## 2. Rules
1. Every PII field MUST have an owner module and legal purpose.
2. Retention periods align with Turkish Tax Procedure Law (10 years) and KVKK.
3. Anonymization: irreversible hashing or deletion of identifying fields.
4. Access: role-based, least privilege.

## 3. Affected Tasks
- V13-CST-001, V15-KVK-001
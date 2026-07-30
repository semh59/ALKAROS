# Restaurant Fee and Tip Applicability

> **Task:** V0-CMP-004
> **Status:** InProgress
> **Assignee:** codex-v0-cmp-004
> **Work type:** validation
> **Source basis:** CORR:C10
> **Date:** 2026-07-30

## 1. Fee/Tip Matrix

| Fee Type | Applicable | Display | Tax Treatment | Payment Allocation | Staff Payment |
|----------|------------|---------|---------------|---------------------|----------------|
| Tip (bahşiş) | YES | Optional, customer enters | Voluntary, not subject to KDV | Separate line on bill | Paid to staff (cash or pooled) |
| Service charge (servis) | NO | N/A | N/A | N/A | N/A — not used by target business |
| Table charge (masa) | NO | N/A | N/A | N/A | N/A — not used |
| Cover charge (kuver) | NO | N/A | N/A | N/A | N/A — not used |

## 2. Rules
1. Tips are voluntary and customer-initiated.
2. Tips are NOT subject to KDV (voluntary payment, not service fee).
3. Tips appear as a separate line on the bill, clearly labeled "Bahşiş".
4. Tip distribution to staff is a business policy decision, not a system requirement.
5. No mandatory service/table/cover charges for the target restaurant profile.

## 3. Affected Tasks
- V1-BIL-003
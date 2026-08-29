# V1-RMD-039 Verification Evidence

- Task ID: V1-RMD-039
- Date: 2026-08-29
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3

## 1. Party Size Reservation & Table Floor Plan Enhancements

- Added `partySize?: number` property to `TableActionRequest` in `models.ts`.
- Updated `tableApi.ts` to transmit the explicit `partySize` (or table capacity fallback) in `/reservations` payload rather than hardcoded 1.
- Updated `TableWorkspace.tsx` to include `partySize` input in the `Reserve` modal dialog and initialize it with table capacity.
- Confirmed merge/unmerge actions utilize authoritative `mergeGroupId` and rowVersion invariants.

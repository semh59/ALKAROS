# V1-RMD-912 reviews

## qa-guvenlik-yetki
- No finding: atomicity, FK-violation rollback, unknown-record path, completeness of actor/ids/correlation id, no secrets in the event bodies. All five decision paths (grant approve/deny, delegation create/revoke, tightening clear) are audited.
- Low: the old non-transaction repository overloads stay on the interfaces. They are used by module tests, and production has no other caller; accepted.
- Low: a revoke or clear of an unknown or already finished record writes no event; intended (nothing was decided).

## qa-test-kalitesi
- Status codes for create, revoke, repeat revoke, clear, repeat clear were not asserted: added. Event for an unknown id: added. expiresAt value now asserted (1 s tolerance).
- Rollback is tested for the audit-store failure of all three decisions in one test; the individual atomicity mutants (1, 2, 3) are killed.

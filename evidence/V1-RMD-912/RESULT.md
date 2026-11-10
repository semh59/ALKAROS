# V1-RMD-912 result

Delegation create, delegation revoke and behavioural tightening clear are now written to the audit trail in the same transaction as the decision (events authorization.delegation.created, authorization.delegation.revoked, authorization.tightening.cleared; actor, record id, permission code, grantee, limit, expiry; correlation id from the request). A record that cannot be written means the decision is not made; an unknown or already revoked/cleared record writes nothing. The tightening clear was not in the coordinator's list but is the same gap class (a manager decision in the same trail), so it is closed here.

- Tests: 3 new (created event contents, revoke and clear events with repeat writing nothing, all three refused when the audit store fails). Red before (red.log), Authorization 18/18 and Identity delegation/tightening 51/51 after.
- Mutations: 5 killed (mutations.txt).
- Shared helper AppendAuditAsync now also serves the grant decision from 907.

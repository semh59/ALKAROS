# V1-RMD-917 result

Removed the methods that only tests still called (production callers re-checked by search before deleting): IAuthorizationGrantRepository.ResolveAsync (no transaction), IAuthorizationDelegationRepository.CreateAsync and RevokeAsync (bool), IBehaviouralTighteningRepository.ClearAsync and IBehaviouralTighteningService.ClearAsync (no transaction). The transaction overloads of V1-RMD-907 and V1-RMD-912 are now the only versions and hold the implementation (no wrapper layer).

Tests moved to the real path: each affected test class has a small helper that opens a connection and transaction, calls the transaction method and commits; expectations are unchanged. Red step: with the methods removed, the old tests no longer compile (that was the signal that every call site moved).

Results: Identity authorization 260/260 (3 new rollback tests), Host authorization (decision endpoints) 18/18. Mutations: 4 killed (mutations.txt).

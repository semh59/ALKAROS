# V1-RMD-917 reviews

## qa-sinir-mimari
- No finding: no removed signature is referenced anywhere in src or tests; no member was left unused by the change; docs on the interfaces match; behaviour unchanged (the core bodies are the same, the nullable transaction parameter was always filled in production).

## qa-test-kalitesi
- No finding on equivalence of the moved tests. Gap noted and closed: no test showed that an uncommitted transaction leaves nothing behind; added three rollback tests (grant resolution, delegation create and revoke, tightening clear), each also proving the record can still be decided afterwards.
- Not added: a mutant that ignores the transaction argument (Npgsql uses the connection's open transaction anyway, so that mutant has no effect); a concurrent double-resolve test (the existing lock test and the single-winner condition cover it).

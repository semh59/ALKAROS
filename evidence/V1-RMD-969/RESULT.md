# V1-RMD-969 Result

Status: Done.

- ApiConventions went red when V1-RMD-901 added POST .../receipt/original-reset without an idempotency key. The endpoint is now in the allowlist with an honest reason, and a new test proves the claim (double reset: second is 409 and records nothing; a reset after a new print records again).
- Tests: ApiConventions 17/17; receipt reset tests 6/6; 2 mutants killed.
- Open product risk (named in the allowlist reason): a lost reset response followed by a reprint and a retry gives the original receipt back a second time (audited, counted toward the reset limit).

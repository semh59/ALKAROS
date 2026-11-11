# V1-RMD-973 Result

Status: Done.

- The original-receipt reset takes a required IdempotencyKey. A resend with the same key (lost answer, even after a new print) returns the first result and resets nothing; without a key the answer is 400. The allowlist entry is gone.
- The cashier page keeps the key across a lost answer and takes a new one once the server has answered.
- Tests: BillReceiptHttpTests 29/29, ApiConventions 17/17, split-payment-receipt-reset 16/16; 3 mutants killed.

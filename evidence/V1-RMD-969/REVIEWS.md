# V1-RMD-969 Reviews

## qa-api-uclar
- Verified in code: the endpoint needs payments.refund.approve and reads no key; a repeat straight after a reset finds nothing to reset and answers 409 RECEIPT_NOT_PRINTED with no second audit record (row lock also serializes two concurrent calls).
- Medium: if the reset response is lost and a print happens in between, the repeat is a fresh reset and gives the original back once more -> the allowlist reason now names this as a known product risk (each reset keeps its own audit record and counts toward the reset limit), like the print entry does. A real idempotency key would need a store for answers; not done here.

## qa-test-kalitesi
- Medium: the claim was unproven -> a test resets twice in a row (second is 409 RECEIPT_NOT_PRINTED, one audit record) and resets again after a new print (second record) (M2).

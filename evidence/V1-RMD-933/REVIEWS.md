# V1-RMD-933 Reviews

## qa-frontend
No findings. The five fixed sentences are letter-for-letter the server's; the pass-through codes all carry Turkish server sentences, so no English or status text leaks. Noted, pre-existing and outside this task: UNSUPPORTED_BILL_STATE always reads "split design cannot be changed" although the server uses it for other situations too (planned as its own task).

## qa-test-kalitesi
Medium fixed: a code the literal pattern could not read (DISCOUNT_REASON_INVALID, interpolation with quotes) was skipped silently; now every code the exceptions are built with is checked and unreadable messages count as per-request sentences, so the reason code joined the pass-through list. Medium fixed: merge exclusion is an explicit list and an unlisted merge code fails. Added a named list of must-find codes and mutants M6-M8. Accepted: a code answered by two sentences keeps the first source's sentence for comparison.

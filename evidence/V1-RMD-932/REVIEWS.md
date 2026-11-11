# V1-RMD-932 Reviews

## qa-backend
No other path opens a card record (the tender endpoint is the only entry; resolve endpoints are unaffected, existing unresolved records still work after a terminal is removed). Medium fixed: Turkish sentence for the new code added to the page dictionary; selected card is dropped when the card method disappears. Accepted: an idempotent replay of an earlier card request after the terminal record is removed gets 409 instead of the stored result (the unresolved-payment lock still prevents a second charge).

## qa-test-kalitesi
Medium fixed: tests for an older server without the flag, for the refused-payment text, for the selection reset and for the summary refresh; mutants M4-M6 added. Accepted: no explicit assertion on the unsettled field after the refusal (payment count 0 and the follow-up EFT payment prove no lock); no audit event is written for this refusal (like other pre-validation refusals).

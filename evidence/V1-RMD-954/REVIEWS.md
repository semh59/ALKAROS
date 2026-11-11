# V1-RMD-954 Reviews

## qa-frontend
- High: the first version resent through submitDiscount/submitComp, which redrew the form every 5 s (focus lost, "Gönderiliyor…" flashing, notice cleared) -> rewritten as a silent probe (probeApproval); only a decided answer is shown, via the same answer handlers as the button (discountAnswered, compAnswered).
- Medium: a validation error could loop forever; a network or 5xx failure flashed an error every 5 s -> the probe sends the stored request body and shows nothing for "still waiting" and for server failures (tested).
- Medium: discount and comp waiting together -> both are probed independently.
- Medium: a payment in flight, a hidden tab -> the probe waits for both (hidden tab tested).
- Low: comp answer has no stale-form check -> pre-existing, not changed here.

## qa-test-kalitesi
- High/Medium: two waiting requests, refusal stops the poll, comp edit stops the poll, a probe while one is unanswered -> tests added for all except the two-waiting-at-once case (code path is two independent ifs).
- Low: boundary of 4999 ms, timer cleanup on screen change -> left out (equivalent behaviour).

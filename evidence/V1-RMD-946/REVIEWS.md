# V1-RMD-946 Reviews

## qa-frontend
- High: the refresh bumped the statement counter and could drop the answer of a customer being opened (the previous customer's limit drafts stayed) -> the refresh no longer bumps it and applies its answer only when no newer choice happened (M1, test with a slow answer).
- Medium: focus lost on redraw -> the focused field is focused again (number fields carry no selection; M2).
- Medium: a failed list refresh called the shared failure handler (banner, busy reset) -> the refresh has its own quiet path; a failure is logged and the screen stays (tests).
- Low: drafts are not refreshed (kept on purpose, typed text is protected); only visibilitychange is watched (it also fires on page restore).

## qa-test-kalitesi
- Medium: the visibility property leaked between tests and old listeners piled up -> restored and removed after each test.
- Medium: the list refresh was not asserted -> the list row amount is asserted.
- Medium: the hidden-page check was synchronous -> it waits before counting.
- Medium: failure, overtaken answer, focus -> tests added. Left out: busy and not-ready phase skips (a one-line guard of the same shape as the visibility guard).

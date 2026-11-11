# V1-RMD-952 Reviews

## qa-frontend
- Medium: the question did not say what is approved -> it now names the permission and amount (M7).
- Medium: no focus handling or Escape; the error text is the server sentence on failure -> not changed: the same two-step pattern (BillSplitWorkspace, HangingOrdersPanel) has no focus handling either, and the error path is unchanged from before this task.
- Low: one open question at a time is intended; opening another card takes it over (tested).

## qa-test-kalitesi
- High: Evet bound to the right card was untested -> two-card test (M5).
- Medium: a second card taking the question over; deny while a question is open -> two tests (M6).
- Medium: after a failure the question is closed and Onayla is back -> covered by the error test (M3).
- Left out: axe run, focus, stale id after a list refresh (a refreshed list keeps ids; a vanished card cannot show a stale question).

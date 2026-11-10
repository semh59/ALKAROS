# V1-RMD-918 result

Red: the existing queued-then-invoiced test now expects blocked=0 and the item still Pending; it failed on `blocked=1` (red.log). Fix: `RetentionSelection.StillInClass` is added to the pending read, so a queued no-invoice item whose customer now has an invoice is neither worked nor counted blocked. The item itself is untouched (work items never change); the customer is queued again as the invoiced class when 10 years elapse. The invoice check in the plan block statement stays as a race guard; a second test fires an invoice insert from a trigger on the job row (written after the pending read) and expects blocked=1, not anonymized.
Green: Host KvkkRetentionTests 8/8 (green.log); Privacy.Anonymization 13/13, Privacy.RetentionExecution 15/15. Mutants 3/3 killed (mutations.txt).
Behaviour note: a stale item stays Pending forever by design and no longer appears in any count; balance-blocked items still show as blocked.

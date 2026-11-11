# V1-RMD-943 Reviews

## qa-ux-tasarim
- Low: the sentence said "seçin" while the other choice needs typing -> now "seçin ya da yazın"; the hint carries role="status" like the payment hint.
- Low: four refund buttons (İade iste, Reddet, İadeyi tamamla, Seçilen ürünleri iade et) are off without a hint -> new task.
- Clean: confirm step, hidden handling, preset choice re-render.

## qa-test-kalitesi
- Medium: no test for clearing the typed reason, whitespace only, or the link while enabled (a hidden hint would still be read) -> the link is now set only while the button is off; the test types, clears and sends spaces; M4 kills a stale link.
- Medium: refundBusy not covered -> unchanged behavior of the existing busy flag, accepted.

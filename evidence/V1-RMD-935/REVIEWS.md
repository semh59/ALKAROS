# V1-RMD-935 Reviews

## qa-backend
Consumers: the same count feeds preview, close response, stored open_bills_count and the acknowledgement gate; it only asks for acknowledgement, never blocks. No window filter, like the bill count and the shift close (accepted). Medium noted: a bill-less order stuck in a live status keeps warning every day until it is closed; there is no administration path for bill-less orders (included in the follow-up list as its own item). Low: the SQL link is repeated from the Billing module because Reporting has no reference to it (existing cross-schema pattern); the label "Açık hesap" now also covers bill-less orders (wording task).

## qa-test-kalitesi
Medium fixed: tests for orders linked by order id (not double counted), an order whose only bill is cancelled (counted), and each live status (theory) with mutants M5-M9. Fixed: the two older close tests keep their original order statuses and acknowledge the open items instead of changing statuses. Accepted: bill linked only through bill items is not seeded separately (same shared predicate as BillOrderLink, covered by the shift close tests).

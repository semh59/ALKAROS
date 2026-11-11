# V1-RMD-938 Reviews

## qa-frontend
- Medium: a payment that is not approved fell through to "Eft" in the lookup -> fixed: the server sends the method only for Approved, Refunded and PartiallyRefunded payments; the screen says "Tahsilat" otherwise (server test M3, screen test for the empty method).
- Low: the line always says "Onaylandı" regardless of status -> new task (status text from paymentStatus).
- Low: two method dictionaries with different wording (payment lines and refund lines) -> new task (one dictionary).
- Low: XSS and missing field tests -> text goes through escapeHtml; the empty method is tested.

## qa-api-uclar
Clean. Notes: no other consumer builds or reads this record (PosTerminal ignores the extra field), one extra query per GET, the Card and Account server paths use the shared lookup (own tests exist for the receipt and refunds).

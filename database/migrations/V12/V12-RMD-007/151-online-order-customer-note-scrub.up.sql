-- V12-RMD-007: the customer's order-level note (often a phone number or an address) was copied into
-- orders.notes in plain text as "Yemeksepeti <code>: <note>". It stays available in the encrypted webhook payload
-- (opened only for an audited view), so the plain copy is removed from every online order.
UPDATE orders.orders
SET notes = regexp_replace(notes, '^(Yemeksepeti [^:]+): .*$', '\1'),
    updated_at = now()
WHERE source = 'Online'
  AND notes ~ '^Yemeksepeti [^:]+: ';

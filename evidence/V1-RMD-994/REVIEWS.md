# V1-RMD-994 Reviews

## qa-frontend
Found: the mark was read on every render (consumed before the return effect) -> fixed with a mount/return-only read and a navigation test; the quick-sale fallback reused a hidden table draft -> now explains; logout clears the mark; message after sending a table order fixed.
Open: after a page refresh the table order is no longer the cart (the mark is single-use); the mutfaksız ekleme path stays silent when the cart is hidden.

## qa-ux-tasarim
Applied: one-tap "Hızlı satışa geç", table-specific send message.
Open: "Tahsil et" does not name the table or amount; header wording (two labels for the same table); the mobile bar shows no table.

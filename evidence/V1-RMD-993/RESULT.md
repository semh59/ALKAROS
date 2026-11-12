# V1-RMD-993 Result

"Masada sipariş aç" no longer fails because of an old empty quick-sale draft. When the terminal's active order is a draft without a table and without any product line, starting the next order cancels it in the same transaction (status, cancelled_at, updated_at and a status history row are written) and goes on. A draft that holds a product still blocks, now with a message that says where to act ("satış ekranında onu gönderin ya da içindeki ürünleri kaldırın").

- red.log: the empty-draft test fails with "Terminal already has an active order". green.log: DualScreenStoreTests 43 of 43 pass.
- mutations.txt: 4 mutations killed (items ignored, result ignored, table drafts discarded too, cancelled_at not written), files restored.

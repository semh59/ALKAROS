# V1-RMD-983 Result

The cashier screen shows "Mutfağa göndermeden hesaba ekle" to holders of orders.add-without-kitchen, only on a sent (non-draft) order. A reason is chosen (Garson unuttu / Müşteri istedi / Diğer); each product tap posts to the kitchen-less endpoint with the current row version and the reason. Products with options are refused with a message. The mode is left with "Normal eklemeye dön", the picker with "Vazgeç"; it also resets when the order or its editable state changes.

- red.log: 3 of 4 first tests fail before the UI. green.log: 6 of 6 pass.
- related.log: all Cashier route tests, 77 of 77 pass. tsc --noEmit: clean.
- mutations.txt: 5 mutations, all killed, files restored (cmp).

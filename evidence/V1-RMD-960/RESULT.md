# V1-RMD-960 Result

Status: Done.

- A product with a price on its own record and no dated price no longer reads "Geçerli fiyat yok; satılabilir değil." The details now say "Ürün kaydındaki satış fiyatı: X TRY; tarihli fiyat kaydı yok." The not-sellable sentence shows only when the product record itself has no price.
- Verified in code that the till sells on the product price and that the recompute job keeps a seed price.
- Tests: catalog folder 49/49; 4 mutants killed.
- Open low note: the price timeline does not mark which dated price is effective now.

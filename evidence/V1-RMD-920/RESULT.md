# V1-RMD-920 result

Report finding: quick-sale `POST /terminals/{t}/orders/{o}/submit` answered 500 "İşlem tamamlanamadı." for a product without stock setup. Cause: the three stock rule exceptions were mapped only in the Orders area; the terminal route fell to the Host default. Now the Host default maps them to 409 (PRODUCT_STOCK_NOT_CONFIGURED, INSUFFICIENT_STOCK, STOCK_ITEM_MISCONFIGURED) with the Turkish sentence naming the product; both mappings read the sentence from the exception classes (single source).
Red: new test returned 500 (red.log). Green: Host.Errors tests 231/231. Mutants 2/2 killed.
Not done here (product decision, asked in the final report): whether a product without stock tracking may be sold at all.

# V1-RMD-1062 RESULT

- Silinen olu kurallar (split-payment.css): `.sp-line-amount`, `.sp-btn-ghost` (+ disabled secicisi), `.sp-alert-success`. Son ikisi gorevin Goal'indeki tek kuralin ayni olu-kod sinifidir; genel test bunlari da yakaladi.
- Kanit: Cashier wwwroot (split-payment.js, index.html, customer-accounts.js) ve src/Clients altinda uc sinif icin 0 eslesme; dinamik sinif birlestirme yok.
- Test: split-payment-cashier-rules.test.js sonuna "kullanilmayan sp- sinifi" testi (tam ad eslesmesi, sp-line vs sp-line-method yanilgisi yok). Kirmizi: red.log. Yesil: green-all.log (split-payment + customer-accounts, 28 dosya, 321 test).
- Mutasyon: mutations.txt (M1-M3 oldu).
- Not: tam StaticApps kosusunda yuk altinda bir indirim testi (split-payment-discount-pending) zamanlama nedeniyle bir kez kirildi; tek basina 3/3 yesil, CSS ile ilgisiz.

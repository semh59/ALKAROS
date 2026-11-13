# V1-RMD-1034 RESULT

Mevcut durum dogrulandi: Bilgi Fisi'nde (split-payment.js receiptHtml) her mutfaksiz satirin altinda "Kasada eklendi" satiri basiliyordu (eski test bunu bilerek ariyordu). Musteri ekraninda yoktu: CustomerDisplayLineDto izin listesi (CustomerDisplayContractTests, 3/3 gecti, customer-display-contract.log) neden alani tasimaz.
Duzeltme: receiptHtml'deki tek satir kaldirildi. Kasiyer ekrani notu (renderBillContext), PosTerminal masa paneli ve bekleyen hesaplar degismedi; /receipt cevabindaki addedWithoutKitchenReason alani kasiyer ekrani notu icin gerekli oldugundan kaldi (qa Dusuk gozlem, kabul).
Kanit: red.log (yeni test kirmizi), green.log StaticApps split-payment 24 dosya/268 test gecti; mutations.txt 2 mutant killed (fise notu geri koy; kasiyer notunu sil); client-build-gate: Cashier'in package.json'u yok, PosTerminal degismedi (log).
Elle senaryo (Semih): masaya urun gonder, kasadan "mutfaga gondermeden ekle" ile bir urun ekle, ode, "Fis yazdir": fiste urun satiri var, altinda "Kasada eklendi" yok; odeme ekraninda notu hala gorulur.

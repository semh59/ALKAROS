# V1-RMD-978 incelemeleri

## 1. qa-backend
Bulgular: (Yuksek) Submitted kilidinde masa ve acik hesap kosulu yok; (Yuksek) Editable=true "Tahsil et" dugmesini gizliyor; (Orta) musteri ekrani Editable ile sapma; (Dusuk) XML yorumlari.
Aksiyon: kilit artik yalniz masaya bagli ve hesapsiz Submitted siparisi kabul eder; Editable yalniz gonderilmemis kalem varken true, yeni AcceptsRound alani ile urun dokunusu devam eder; hesap acikken ikisi de false; XML yorumlari guncellendi. Testler: ASubmittedQuickSaleWithoutATableTakesNoMoreItems, ASubmittedTableOrderWithAnOpenBillIsStillRefused, Cashier.first-touch yeni test. Musteri ekrani testleri 37/37 gecti (display.log).

## 2. qa-test-kalitesi
Bulgular: bill'li masa ve masasiz siparis testsiz; mutant seti dar; zayif assert.
Aksiyon: iki test eklendi, mutantlar M4-M7 eklendi (mutations.txt, hepsi olu), ASentLine... testine AcceptsRound/Editable assert'i eklendi.
Acik: gercek submit yolu yerine SQL ile Submitted kuruldu (kalem durumu Active); gonderim turu ana ekran gorevinde uctan uca kosulacak.

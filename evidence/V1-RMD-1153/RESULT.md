# V1-RMD-1153 sonuc

Kok neden: urun degil test duzenegi. Bekleyen onayli sayfa 5 sn'lik setInterval kurar (split-payment.js ensureApprovalPoll); test bitince DOM silinir ama sayac yasar. split-payment-discount-pending dosyasi 5 sn'yi asinca eski sayfa sonraki testin ortak fetch taklidine ve document'ine dokunur (indirim cevabini tuketir, ekrani eski durumla cizer). Dosya tek basina 3/3 kirmizi, tek test (-t) yesil; onceki hicbir tek test sebep degil. Gercek tarayicida sayfa kapaninca sayac olur. Urun kodu degismedi.
Duzeltme: support/setup.js her testten sonra o testin kurdugu setInterval'leri temizler. Kilit: support/page-timer-teardown.test.js.
Kanit: red-static.log (30/31), green-static.log (44/44), static-full.log (74 dosya, 823 test, 0 kirmizi), mut-M1 ve M2 olduruldu (kopyala-geri al-cmp), M3 esdeger kontrol.
qa: REVIEWS.md.

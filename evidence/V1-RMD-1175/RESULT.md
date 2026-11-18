# V1-RMD-1175 sonuc
Yapildi: iade onay ucuna istege bagli ManagerUsername/ManagerPassword (RefundApproveInPlaceV1). Dolu ise arayan payments.refund.request ister ve istegi acan olmalidir (REFUND_REQUEST_NOT_YOURS), yonetici InPlaceManagerApproval ile dogrulanir (payments.refund.approve, arayandan farkli, ayardan bagimsiz, 3 deneme/15 dk), karar veren=yonetici; nakit iade istegi acanin kasasindan cikar. Bos govde: eski yol aynen (istek acan ile onaylayan ayri kisi kurali korunur). Ekran: karar veremeyen satira 'Yonetici burada onaylasin' (kullanici adi, sifre type=password), sifre state'te tutulmaz.
Celiski: yok.
Kanit: red.log 7/7 kirmizi, red-vitest.log 4 kirmizi, green.log Billing PaymentRefundHttpTests 118/118, green-vitest.log (5/5 yeni, 31/31 eski), mutations.txt (JS 3, sunucu 2 mutant olu).
Risk: sahiplik kontrolu sifre dogrulamadan sonra (basarisiz sayac etkisi kucuk); kart istegi, kasa oturumu yok ve refund.request izni yok yollari bu uc icin ayri test edilmedi.

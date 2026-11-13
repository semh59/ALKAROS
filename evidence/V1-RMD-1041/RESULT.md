# V1-RMD-1041 RESULT

Sorun: PaymentTender.Tests ACancelledBillNoLongerLeavesOrderReceivedOnTheDisplay hesap iptalinden sonra musteri ekrani "Idle" olmuyor diye dusuyordu (HEAD'de de kirmiziydi, 1037 RESULT'ta not edilmisti).
Kok neden: uretim hatasi degil, test eskiydi. CustomerDisplayPaymentProgress.Apply yalniz duzenlenmeyen (Editable=false) siparisi iptal edilmis hesapla bosaltir; test siparisi mutfaga gonderilmemis (Draft kalemli) tohumluyordu, bu siparis duzenleniyor sayilir ve ekranda kalmasi dogrudur (CustomerDisplayStatementTests ayni kurali birim duzeyinde kilitler).
Duzeltme: test mutfaga gonderilmis siparisle (kalemler Active, yeni yardimci MarkOrderItemsSentAsync) calisir, ayni uc assert korunur (Idle, bos satir, 0 toplam); ikinci test gonderilmemis siparisin ekranda kaldigini kilitler. Uretim kodu degismedi.
Kanit: red.log (eski test Idle yerine Active), green.log PaymentTender.Tests projesi, mutations.txt M1-M4 killed.
Acik (urun karari, yapilmadi): iptalden sonra siparis tekrar mutfaga gidip yeni hesap acilmazsa musteri ekrani bos kalir; hesap acilinca duzelir.
Elle senaryo: masaya urun ekle, mutfaga gonder, hesaba gec, Iade ve iptal > Hesabi iptal et (siparisi iptal etme isaretsiz): ikinci ekran bos "Hesap iptal edildi." mesajina doner.

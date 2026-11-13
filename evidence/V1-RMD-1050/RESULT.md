# V1-RMD-1050 RESULT

Kok neden: kasanin aktif siparis okumasi (/orders/active) musteri ekraninin "iptal edilmis hesap = bos ekran" kuralini da uyguluyordu; hesabi iptal edilmis masa siparisi Idle ve satirsiz donuyor, satis ekrani Idle siparisi sepet saymayip bos Hizli satis aciyordu.
Duzeltme: CustomerDisplayPaymentProgress.Apply masa numarasi tasiyan (yalniz kasa okumasi tasir) siparisi iptal edilmis hesapla da Active birakir; masa numarasiz Hizli satis ve musteri ekrani (V1-RMD-1048) Idle kalir.
Kanit: red-http.log (kasa okumasi Active yerine Idle), green-unit.log CustomerDisplay 39/39, green-http.log 5/5 (1048 musteri ekrani testleri dahil), mutations.txt M1-M4 killed.
Elle senaryo: masaya urun ekle, mutfaga gonder, hesabi ac, iptal et (Iade ve iptal); masa planinda ayni masada "Urun ekle": satis ekrani bos Hizli satis yerine masanin siparisini sepet olarak acar; musteri ekrani karsilamada kalir.
Not: Endpoints.cs degismedi (ilk tasarimdaki forTill bayragi gereksiz bulunup kaldirildi).
Urun karari (listelenir): iptal edilen hesabin eski gonderilmis kalemleri sepette gorunur ve cikarilamaz, "hesap iptal edildi" uyarisi yok; musteri ekrani yeni turda yeni hesaba kadar Idle kalir.

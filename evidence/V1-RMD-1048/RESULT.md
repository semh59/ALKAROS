# V1-RMD-1048 RESULT

Karar (Semih 2026-11-13): hesap iptal edilince musteri ekrani hemen bosalsin; 1041'de bulunan gonderilmemis kalemli siparisin ekranda kalmasi kalksin.
Duzeltme: CustomerDisplayPaymentProgress.Apply artik canli hesabi kalmamis ve iptal edilmis hesabi olan Active siparisi, siparis duzenleniyor olsa da (Editable) Idle'a ("Hesap iptal edildi.", bos satir, 0 toplam) dondurur. Iptal edilmemis hesapla duzenlenen siparis (anyBillCancelled=false) ve Completed siparis degismez.
Testler: birim test eski "duzenlenen siparis iptalden etkilenmez" kuralini tersine cevirdi, yeni "iptal yokken duzenlenen siparis aynen kalir" testi eklendi; HTTP testi gonderilmemis kalemli siparis icin Idle bekler.
Kanit: red-unit.log/red-http.log (iki test kirmizi), green-unit.log CustomerDisplay 38/38, green-http.log PaymentTender.Tests 125/125, mutations.txt M1-M3 killed (M1 birim ve HTTP).
Elle senaryo: masaya urun ekle (mutfaga gonderme), hesaba gec, ikinci ekrana goster, Iade ve iptal > Hesabi iptal et: ikinci ekran hemen karsilama ekranina doner.
qa-test-kalitesi: bulgu yok. qa-uctan-uca-akis: orta, urun karari: iptalden sonra ayni siparise kalem eklenirse ekran yeni hesap acilana kadar Idle kalir ve yeni kalemleri gostermez (snapshot her okumada siparisten uretilir; iptal ani saklanmaz). Yeni hesap acilinca duzelir. Semih'in karari geregi yapilmadi; sonda urun karari olarak listelenir.
Dogrulama turu: bulgu urun karari, kod degismedi; testler ve mutantlar yukaridaki gibi.

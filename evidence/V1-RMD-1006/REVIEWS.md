# V1-RMD-1006 incelemeleri

## qa-frontend
Yuksek/kritik yok. Duzeltilenler: kullanilmayan kitchenState alani kaldirildi; olu CSS (facts/pointer) silindi; markTableCart(false) eski mutfaksiz niyeti siliyor (test + mutasyon eklendi); dokunma hedefleri 48px.
Kabul edilen: mutfaksiz niyet yalniz satis ekrani her acilisinda okunur (yeni mount veya geri donus); bridge'deki Masa tasi ile menudeki Masa degistir adi tek eylemin iki yuzu (menude yalniz panel yokken/ek komutlar).

## qa-ux-tasarim
Yuksek bulgu (gonderilmemis kalem isaretsiz) duzeltildi: Draft kalemde Mutfaga gonderilmedi etiketi; gonderilmemis kalem varken panel metni mutfaga gondermeyi soyler; Diger islemler menusu ok isareti aldi. Mini satir esit genislikte.
Kabul edilen: Iptal / iade yalniz hesap acikken, kitchenless dugmesi yalniz gonderilmis siparisde gorunur (is kurali); 1024x768'de panel kaydirmayla tamamlanir; Boş/Müsait ve Toplanıyor/Temizleniyor adlandirma kararlari ayri konu.

## qa-backend
Musteri ekrani sozlesmesi bozulmuyor (Unsent yalniz kasa icin, WhenWritingDefault); SQL dogru. Acik dusuk: Unsent=true serilestirme testi ve tum satirlarin Draft oldugu durum testi yok (CustomerDisplayContractTests bu gorevin yuzeyinde degil).

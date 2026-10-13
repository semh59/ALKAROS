# V1-RMD-513 kanıtı

Sonuç: hesap bölme ekranındaki İngilizce ve teknik sızıntılar kapatıldı.

## Davranış
- Yükleniyor metni "Güncel hesap ve dağıtımlar alınıyor…".
- İkincil etiket yokken sahip türü Türkçe ("Kalıcı sandalye" / "Kişi"); ham "Seat/Person" görünmez.
- Başlıkta satır sürüm numarası yok; "Güncellendi 10:05" yerel saat veya "Henüz güncellenmedi".
- Çakışma uyarısı HTTP durumundan (409) anlaşılır; sunucu mesajındaki kelimelerden tahmin edilmez. 422 gibi doğrulama hataları sunucunun Türkçe mesajını gösterir.

## Kanıt
- `test-runs.log`, `mutations.log`: 8 mutant, hepsi killed.

## Açık
- Sunucunun döndüğü doğrulama mesajları Türkçe sözleşmesine güvenir; ham HTTP kodu gösterilmez.

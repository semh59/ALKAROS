# V1-RMD-512 kanıtı

Sonuç: UI departmanının kasiyer denetiminde kodda doğrulanan hatalar kapatıldı.

## Davranış (kasa sayfası)
- Ödeme "Declined" dönerse "Ödeme reddedildi. Başka bir yöntem deneyin." gösterilir.
- Hesap yüklenirken 401 dışı sunucu hatası ve ağ kopması "Hesap Okunamadı" + "Tekrar dene" ekranına gider; giriş ekranı yalnız 401'de.
- Hesabı iptal et ve iadeyi onayla ikinci adım ister ("Emin misiniz?" / "Vazgeç"); gerekçe, ret notu ve fiş numarası boşken ilgili düğme pasif.
- İade/iptal 409 dönerse hesap yeniden yüklenir; iade listesi okunamazsa (403 hariç) iade satırları gizlenip uyarı gösterilir.
- "Ödendi" ve "İptal edildi" ekranlarında "Bekleyen hesaplara dön" bağlantısı; tahsilatta ağ hatasında "gerçekleşmiş olabilir" uyarısı.

## Davranış (PosTerminal iade paneli)
- Çözümlenmemiş ödeme varken iptal sunulmaz; iptal/onay ikinci adım ister; boş gerekçede düğmeler pasif; liste okunamazsa iade satırı yok (403 hariç); eylem sonrası yenileme düşse bile panel ve bildirim korunur; 409'da veri yeniden yüklenir.

## Kanıt
- `test-runs.log`, `mutations.log`: 24 mutant, hepsi killed (ilk turda 4 hayatta kalan → test eklendi).

## Açık
- Kasa sayfası yetkiye göre düğme gizlemiyor (oturum ucu yetki listesi döndürmüyor); sunucu 403 ile reddediyor.
- Hazır neden listesi, fiş yazdırma, para üstü, kısmi iade, bölme-tahsilat birleştirme ürün kararı bekliyor.
- Gerçek tarayıcıda elle deneme yapılmadı (jsdom + enjekte fetch).

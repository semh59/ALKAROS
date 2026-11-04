# V1-RMD-812 - Sonuç

Müşteri ekranı dökümü sırayla ve tutarlı gösterilir, tamamlanan ödemede ödenen toplam yazar, yazılar uzaktan okunur, uzun siparişte liste kendiliğinden sayfa çevirir.

## Değişiklik

- Döküm sırası: Ara toplam, İndirim (−), Bahşiş (+), Ücret (+), Hesap toplamı, Ödenen (−), İçindeki KDV; satır yalnız değeri varsa görünür, Hesap toplamı yalnız ayar veya ödeme varsa. Büyük rakam KALAN TUTAR / ÖDENECEK TOPLAM.
- Kalem: net tutar, indirimliyse üstü çizili eski tutar ve "İndirim −x"; ikram "İkram" ve 0,00.
- Tamamlanan ekran: "Ödenen toplam" (ödeme kaydı varsa). "Canlı güncelleme · v{revision}" kalktı.
- Uzun liste: "+N kalem daha" göstergesi (ResizeObserver ve kaydırmayla yeniden ölçülür), 6 sn'de bir ilk kesik kalemden sayfa çevirir, sonda başa döner; azaltılmış harekette animasyonsuz. Kaydırma çubuğu gizli.
- Yerleşim: etkin ekran pencere yüksekliğine sığar (taşma yok), bağlantı bandı akışta ve başlığı örtmez, 700 px altı için sıkı düzen; yazı adı/tutar en az 28 px, alt satır en az 16 px; kontrastlar >= 4,5:1.
- F0413: Kasiyer fişi hesap değil sipariş verisi gösterir, bahşiş yoktur; "Ara toplam" etiketi satırlar toplamı tuttuğu için doğrudur (plan kapsamından Cashier çıkarıldı). F0836 (boş ekran/ekran koruyucu resim, video, yedek karşılama) ve F0803 (9 sn'de veri durur, 10 sn'de kalkar) testleri eklendi. F1016: rozet kontrastı en kötü durumda (beyaz resim) ölçülür ve test eder.

## Doğrulama

- Kırmızı: `vitest-red.log` (13 yeni test başarısız). Yeşil: PosTerminal vitest 88 dosya / 919 test, tsc 0, lint 0 hata (`vitest-final.log`).
- Playwright (vite geliştirme sunucusu, sahte API yanıtı; canlı yığına veri yazılmadı): 1920x1080 ve 1024x600 ekran görüntüleri `screen-*.png`. Ölçüm: belge taşması yok (scrollHeight = pencere), toplam kartı pencere içinde (1024x600'de alt kenar 464 px) ve içi kaydırmasız, ad 33,6 / 28 px, alt satır 19,2 / 16 px, otomatik sayfa çevirme çalışıyor.
- Mutasyon (`mut/results.txt`, hepsi `cmp` ile geri alındı): bahşiş satırı, sıra, ikram koşulu, ödenen toplam, gösterge, azaltılmış hareket, başa dönüş, yazı boyutu, banner konumu, kontrast, Hesap toplamı koşulu (m1-m11); sayfa hizası, ResizeObserver bağlantısı/temizliği, çizili tutar, net tutar, çubuk gizleme (n1-n6, p1-p2): hepsi test kırdı.
- Atlanan test yok. Sınır: kaydırma ölçümü jsdom'da sahte yerleşimle sınanır, gerçek yerleşim Playwright ile görüldü; gerçek kasa-müşteri ekranı eşleşmesiyle elle deneme yapılmadı.

## İncelemeler

- qa-frontend: bağlantı bandı kenar boşluğu kısa ekranda, dar kırılımda yazı boyutu, marka özgüllüğü, ResizeObserver ve temizlik testleri (hepsi düzeltildi). Reddedilen: KDV'yi Ara toplam'ın altına almak (planda KDV en altta küçük).
- qa-ux-tasarim: çubuk gürültüsü, satıra oturmayan sayfa, ikram/indirim tutarının belirsizliği, küçük döküm/sipariş no yazısı, 1024'te ad sarması (hepsi düzeltildi).
- Doğrulama turu (qa-bulgu-dogrulayici): 8 maddeden 7 kapandı; açık kalan bant kenar boşluğu (kısa ekran kuralı sonradan ezilmişti) kural sırası düzeltilerek ve testle kapandı; yeni bulgular (sayfa hizası testi iki yöntemi ayırmıyordu, listeden uzun tek kalem sayfayı kilitleyebilirdi, `auto` sütun hizayı kaydırıyordu) düzeltildi (120 px ve 600 px satır testleri, sütun 200 px).

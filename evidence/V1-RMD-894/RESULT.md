# V1-RMD-894 sonucu

Hiç tahsilat yapılmadan kapanan hesapta kasiyer ekranı artık "Hesap Ödendi / Bu hesabın tamamı tahsil edildi." demiyor.

## Metinler (split-payment.js `closedHeading`)
- Tahsilat var (alınan toplam > 0): "Hesap Ödendi"; alt metin düşüş yoksa "Bu hesabın tamamı tahsil edildi.", düşüş varsa "Bu hesap tahsilat ve ikramla / indirimle kapandı." ya da "Bu hesap tahsilat, indirim ve ikramla kapandı."
- Tahsilat yok, düşüşlerin hepsi ikram: "İkramla Kapandı" / "Bu hesabın tamamı ikram edildi; tahsilat yapılmadı."
- Tahsilat yok, hepsi indirim: "İndirimle Kapandı" / "Bu hesap indirimle kapandı; tahsilat yapılmadı."
- Tahsilat yok, ikisi karışık: "İndirim ve İkramla Kapandı" / "Bu hesap indirim ve ikramla kapandı; tahsilat yapılmadı."
- Tahsilat yok, düşüş de yok (sıfır tutarlı hesap): "Hesap Kapandı" / "Bu hesapta tahsil edilecek tutar kalmadı."
Bahşiş ve ek ücret düşüş sayılmaz. Gerekçe ikram kataloğundaysa düşüş ikramdır (ekranın mevcut ayrımıyla aynı).

## Kanıt
Kırmızı red-1.log (4 yeni davranış kırmızı, 2 kontrol yeşil). Yeşil: tüm split-payment testleri final-all.log. Mutasyon (mutations.txt, tur 1 ve tur 2): tahsilat yok sayma, boş düşüş dalı, "herhangi ikram", karışık, bahşişi düşüş sayma, ikram ayrımı, tahsilatlı hesapta düşüş metni: hepsi öldü, cmp ile geri alındı.

## İnceleme sonrası
- Kısmen tahsil edilip kalanı ikram/indirimle kapanan hesapta başlık "Hesap Ödendi" kalır, alt metin artık "Bu hesap tahsilat ve ikramla kapandı." (indirim: "tahsilat ve indirimle", ikisi: "tahsilat, indirim ve ikramla"); eskiden "tamamı tahsil edildi" yanlış iddiasıydı. Yalnız bahşişli tahsilatlı hesapta eski metin kalır.

## Açık sınırlar (bu görev dışı)
- Tahsilatsız kapanan hesapta "Fişi yazdır" ve iade bölümü hâlâ görünür; ikram hesabı için fişin yasal/ürün anlamı ve iade bölümünün boş hesapta nasıl görüneceği ürün kararıdır.
- Ek (ikram/indirim) listesi okunamazsa (ağ hatası) tahsilatsız hesap "Hesap Kapandı" der; yanlış iddia değil, bilgi eksik.
- Yoklamayla yenilenen özet ekleri yenilemez; başka terminalde yapılan ikram bu ekranda sonraki yüklemeye kadar başlığa yansımaz.

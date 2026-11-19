# Pilot günü kontrol listesi (işletme sahibi için)

Tırnak içindeki yazılar ekranda aynen böyle yazar. "Yönetim" bölümüne yönetici hesabıyla girilir: satış ekranında **Diğer ▾** menüsünde **Yönetim**.

## Pilottan önce bir kez (henüz denenmemiş maddeler)
2026-11-18 pilot deneme listesine (kasa-review 35) göre şu maddeler henüz denenmemiştir; pilottan önce gerçek cihazlarla yapılmalıdır:
- [ ] Sunucu bilgisayar yeniden başlatılınca sistem kendiliğinden açılıyor mu? (Denenmedi. Yeniden başlatın, birkaç dakika sonra aşağıdaki sabah 2. maddeyi uygulayın.)
- [ ] Gerçek yazıcı ve gerçek para çekmecesi denendi mi? (İlk nakit satışta çekmece açılıyor mu bakın.)
- [ ] Kasiyer bilgisayarı, garson telefonu ve mutfak ekranı gerçek cihazlarında denendi mi?
- [ ] Cihazlarda adres çubuğunda güvenlik kilidi görünüyor mu?
- [ ] Sistem kapanırsa telefona haber veren izleme kuruldu mu? (Denenmedi; teknik destekle kurun.)
- [ ] Elle satış defteri basıldı mı, mali müşavire gösterildi mi, kasiyer eğitildi mi?

## Sabah açılış
1. [ ] Sunucu bilgisayar açık, ağa bağlı, uykuya geçmiyor.
2. [ ] Kasa ekranı açılıyor ve giriş ekranında **Kasa sunucusu hazır** yazıyor. "Kasa sunucusuna ulaşılamıyor" yazıyorsa birkaç dakika bekleyin; geçmezse teknik destek arayın.
3. [ ] Yazıcı açık, kâğıt var. Çekmece takılı ve kapalı.
4. [ ] Yedek diski takılı. Yönetim > **Güvenlik** > **Yedekleme** tablosunda **Durum** sütunu **Hedefe uygun**. Yönetim > **Hatalar**'da "Makine dışı kopya kullanılamıyor" ya da "Yedekleme başarısız" yok.
5. [ ] Kasiyer giriş yaptı ve vardiyayı açtı: sağ üstte **Kasa: Açık**. **Açılış Tutarı** çekmecedeki parayla aynı. Sarı bantta "Kasa oturumu kapalı" yazıyorsa vardiya açılmamıştır.
6. [ ] Kasiyerin elle satış defteri ve kalemi yanında.

## Gün içinde
- [ ] Ekranda "Bağlantı yok" yazarsa kasiyer sizi çağırır; kesintiyi kontrol edin (yönlendirici, kablo, sunucu). Birkaç dakikada düzelmezse elle satış defterine izin verin.
- [ ] Pilot geri bildirim formunu gün içinde birlikte doldurun.

## Akşam kapanış
1. [ ] **Masalar** ekranında dolu ya da "Ödeme bekliyor" masa kalmadı. Kalmışsa kapanışta "İşletmede açık işlem var" uyarısı çıkar; önce onları çözün.
2. [ ] Kasiyer kasayı kapattı: **Kasayı Kapat: Sayıma Başla**, parayı saydı, **Kasayı Kapat**. "Kasa Kapatıldı" yazdı.
3. [ ] Fark: Yönetim > **Gün sonu ve mutabakat** > **Kasa oturumları** tablosunda **Beklenen nakit**, **Sayılan nakit**, **Fark**. Fark büyükse kasiyer sizden yanında kullanıcı adı ve şifrenizi girmenizi ister; nedenini sorup yazdırın.
4. [ ] Yedek alındı: Yönetim > **Güvenlik** > **Yedekleme**'de **Durum** hâlâ **Hedefe uygun**. Yedek sistem açıkken saatte bir alınır; bilgisayarı gece kapatırsanız o saatlerde yedek alınmaz.
5. [ ] Yedek diski takılı kalsın. Çıkarırsanız "Makine dışı kopya kullanılamıyor" uyarısı çıkar.
6. [ ] Yönetim > **Hatalar**'da yeni kayıt varsa ekran görüntüsünü alıp forma ekleyin.

## Bir şey ters giderse
- Yedek uyarısı çıktı ya da veri yanlışlıkla silindi: kasiyere işlem almayı bırakmasını söyleyin, açık hesapları kâğıda yazın, teknik desteği arayın. Geri yüklemeyi teknik destek yapar. Teknik destek tel: ................
- Yedek hiç alınmıyor görünüyorsa ya da bilgisayar yandı/çalındıysa yine teknik desteği arayın.

Teknik destek notu: adımlar `docs/operations/yedekten-geri-donus.md` içindedir (bölüm 3, 5 ve 5b).

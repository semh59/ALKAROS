# V1-RMD-1123 sonuc
Dogrulama: rapor dogru; cash-session.js'te 6 catch yalniz "Sunucuya ulasilamadi." diyordu (giris, kasa acma, hareket ozeti, sayim baslatma, sayim kaydi, beklenen tutari yeniden okuma).
Duzeltme: her biri "Baglanti kesildi." ile baslar; yazma islemlerinde islemin kaydedilmis olabilecegini soyler (kasa acma ve sayim baslatma: sayfayi yenileyip kasa durumunu kontrol et; sayim kaydi: sayimi tekrar gonder, sunucu en son sayimi kullanir); okuma/dogrulama yollarinda (giris, hareket ozeti, beklenen tutar) kaydin yapilmadigini ve tekrar denemeyi soyler. Kasa kapama (:633) ve nakit hareketi (:463) mesajlari degismedi.
Kanit: red.log 6 kirmizi; green.log 6 yesil; green-full.log (cash-session* + split-payment-cash*) 12 dosya/186 test yesil; mutations.txt M1-M6 (her mesaj eski haline) killed (cp+cmp).
Acik risk: E2E (tests/E2E) calistirilmadi; 05 spec'i yalniz degismeyen hareket mesajini kontrol eder.
Not: qa-ux-tasarim 463/495/244/278/633 mesajlariyla uc farkli acilis ("Sunucuya ulasilamadi", "Baglanti koptu", "Baglanti kesildi") kaldigini bildirdi; bu mesajlar plan disinda (Out of scope), degistirilmedi.

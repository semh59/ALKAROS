# V1-RMD-1126 sonuc
Dogrulama: cash-session.js'te uc ifade gercekten vardi (Sunucuya ulasilamadi x4, Baglanti koptu x1, Baglanti kesildi x6).
Duzeltme: hepsi "Baglanti kesildi. <ne oldu>; <ne yapilacak>." kalibina cevrildi (oturum durumu, kasa durumu, hareket onayi, onay karari, kasa kapama sonrasi bildirim). Akislar ayni.
Kanit: red.log 3 kirmizi; green.log 9 dosya/146 test yesil; mutations.txt M1-M6 killed (cp+cmp). Not: red sonrasi iki mevcut testin beklentisi (buyuk harf "Kasa kapanmis...", acilis testinde bekleme sirasi) yeni metne uyduruldu.
Acik risk: kalip kaynak-metin testiyle kilitli (en az 11 "Baglanti kesildi." cumlesi); yeni cumle eklemek bu esigi degistirmez.

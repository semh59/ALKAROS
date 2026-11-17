# V1-RMD-1127 sonuc
Dogrulama: gercekti. Sayim gonderilince taslak tutar/not siliniyor, "Sayimi duzelt" modu tek tutara zorlayip kupur girislerini gostermiyordu (kupur sayilari state'te kalsa da ekranda yoktu).
Duzeltme: cash-session.js submitCount taslak tutar/notu silmiyor; recount modu zorlamiyor ve tutari yeniden yazmiyor. Kupur adetleri, tek tutar ve not oldugu gibi dolu gelir. Sayilan tutar zaten fark ekraninda gorunuyordu; beklenen tutar sayim ekraninda gizli kalir (1121 DOM testi yesil, M5 sizinti mutanti killed). Yeni oturum taslaklari resetForNewSession ile eskisi gibi sifirlanir.
Kanit: red.log 4 kirmizi; green.log 9 dosya/147 test yesil; mutations.txt M1-M5 killed (cp+cmp). Mevcut uc testin beklentisi (counted-amount -> denom-100) yeni davranisa uyduruldu.
Acik risk: yok bilinen.

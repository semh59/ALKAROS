# V1-RMD-1075 RESULT
cash-session.css icindeki 11 yazi boyutu bildirimi (12px/13px: eyebrow, durum rozeti, uyari, Acilis Tutari/Saati etiketi, alan etiketi ve ipucu, bos liste, pasif dugme aciklamasi, Listeyi yenile, ozet satiri, hata metni, ikon) 14px yapildi; en kucuk kalan boyut 14px. cash-session.js ve index.html'de yazi boyutu yok.
Test: tests/Clients/StaticApps/cash-session-text-size.test.js (kural cumlesi dokumanda duruyor; her font bildirimi duz px/rem >= 14; JS/HTML'de boyut yok). red.log taban kodda 11 ihlal gosterir.
Kanit: red.log, green-file.log (3 test), green-static.log (StaticApps tum suite), mutations.txt (12 mutant killed).
Olculmedi: gercek tarayicida gorsel ve 440px kartta tasma (kod okumasiyla degerlendirildi); Docker yigini yeniden kurulmadi.

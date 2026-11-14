# V1-RMD-1063 RESULT

Kok neden (yonetici "yetkiniz yok"): backend yetki kurali dogru (yonetici baskasinin oturumunu sayip kapatir: CashCloseSafetyHttpTests.AManagerMayStartCountAndCloseAnotherCashiersSession). Canli yiginda ayni terminale baska tarayicidan yonetici girisi 403 TERMINAL_KEY_MISMATCH donuyor (curl ile uretildi); cash-session.js girisi bu kodu tanimadigi icin her 403'u "Bu islem icin yetkiniz yok" yaziyordu. Kasiyerin kapatamamasi kurala uygun (sahip ya da yonetici); eksik olan ekranin bunu soylemesiydi.
Duzeltme: yeni GET .../cash-sessions/active/owner (kasa cekmecesi yetkisi ister, 404 oturum yoksa); sayfa basligi oturum sahibinin adini yazar; baskasinin oturumunda sahip adi, "oturumu acan kasiyer ya da bir yonetici" cumlesi ve pasif duğmenin hemen ustunde neden gorunur; yonetici icin duğme acik ve "Yonetici olarak ..." notu; TERMINAL_KEY_MISMATCH icin dogru Turkce cumle.
Kanit: red.log, red-host.log, green-file.log, green-host.log (29 test), green-host-final.log, green-all.log (StaticApps 50 dosya / 586 test), mutations.txt (14 killed), mutations-host.txt (3 killed).
Calistirilmayan: CashSession Host projesinin tamami tek seferde (580 sn zaman asimina takildi, exit 124); Safety sinifi + GetActive (29 test) ve duzenlenen test ayri kosuldu.
Ekranda dogrulanmadi: gercek tarayicida gorsel.

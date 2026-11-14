# V1-RMD-1074 RESULT
Neden calismadi (kasiyer denemesi 9): V1-RMD-1063 yalniz kasa oturumu sayfasina sahip adini koydu; V1-RMD-1064 baslik rozetini "Kasa: Baska kullanicida" yapti ama adi istemedi, /active yaniti yalniz cashierUserId tasir.
Duzeltme: oturum baskasindaysa kanca mevcut GET .../cash-sessions/active/owner uc noktasindan adi okur, rozet "Kasa: Baska kullanicida (Ad)" yazar; ad okunamazsa (404, ag hatasi, bozuk govde, bos ad) duz metin kalir; kendi oturumunda ad istenmez. Sunucu degismedi.
Kanit: red.log, green-file.log (30 test), green-all.log (PosTerminal 119 dosya / 1409), green-static.log (StaticApps 595), client-build-gate.log exit 0, mutations.txt (M1-M4, M5b, M6-M8 killed; M5 esdeger).
Ekranda dogrulanmadi: gercek tarayicida gorsel; Docker yigini yeniden kurulmadi. Uzun adin dar baslikta tasmasi olculmedi.

# V1-RMD-1124 sonuc
Dogrulama: iki acik risk gercekti (401 genel "okunamadi" cumlesine dusuyordu; recent-sales.css yazi boyutu kapisinda degildi).
Duzeltme: recentSalesText.sessionExpired ("Oturum suresi doldu, yeniden giris yapin.") 401'de gosterilir (notifySessionExpired cagrisi ayni); recent-sales.css cashierTextSize.test.ts listesine eklendi (css zaten 14 px ustu: kapi yesil).
Kanit: red.log 1 kirmizi (401 genel cumle); green.log 25 yesil; green-full.log 125 dosya/1493 test yesil; client-build-gate exit 0; mutations.txt M1 (401 ayri degil), M2 (12px), M3 (em birimi) killed (cp+cmp). CSS maddesi icin kirmizi test yok (kapi mevcut css ile yesil); yakalayicilik M2/M3 ile kanitli.
Acik risk: 401'de oturum bitis bildirimi (notifySessionExpired) kabuk tarafinda girise yonlendirebilir; cumle yalniz yonlendirme gecikirse gorunur.

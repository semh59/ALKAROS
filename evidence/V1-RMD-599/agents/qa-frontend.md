# V1-RMD-599 qa-frontend raporu (SHA d1dc86b6)

## 1. Envanter (M=15, 15/15 kontrol edildi)
Komut: grep -rniE "help-?request|web-?push|waiter-notif|customer-display|pairing|hubs/" src/Clients (node_modules/dist haric) + el ile ek (push.js, api.js, util.js).
PosTerminal: CustomerDisplay.tsx, CustomerDisplay.test.tsx, CustomerDisplayScreensaverSettings.tsx(+test), api.ts (pairing/snapshot/screensaver uclari), stale.ts, Cashier.tsx (eslestirme diyalogu + help-requests hub), Cashier.help-alerts.test.tsx.
WaiterPwa: sheets/help-request.js, push.js, sw.js, api.js, waiter-app.js (waiter-order-status hub 771-839, profil push dugmesi 686), sheets/profile.js.
Statik: tsc --noEmit exit 0; oxlint (7 dosya) exit 0, 3 uyari (Cashier.tsx:565/727/730, jsx-a11y). Vitest kosulmadi.

## 2. Bulgular (kanit duzeyi: Kod okumasi, aksi belirtilmedikce)
F1 Yuksek - CustomerDisplay.tsx:56-58,203-213; yoklama yalniz "ready" durumunda (:95-96). Ilk acilista/eslestirmede ag hatasi, 5xx veya 429 olursa ekran "BAGLANTI KURULAMADI" durumunda kalir, otomatik yeniden deneme yok; yalniz dokunulan "Tekrar dene" veya tarayici "online" olayi kurtarir. Etki: Host yeniden baslarken musteri ekrani sonsuza dek olu. Oneri: error durumunda geri cekilmeli otomatik refresh.
F2 Orta - CustomerDisplay.tsx:77-82. Eslestirme tamamlama yoklamasinda 409 disi her hata (429, 5xx, tek ag kopmasi) pairing'i iptal edip hata ekranina dusurur; F1 ile birlesince gecici kopma kalici olur.
F3 Orta - CustomerDisplay.tsx:44-51; stale.ts:23. refresh() cakisabilir (5 sn yoklama, hub, online, tamamlama); eski revizyonlu yavas yanit yenisinin ustune yazar (revision karsilastirmasi yok). Etki: musteri ekraninda eski tutar. Oneri: revision geriye gidiyorsa yoksay.
F4 Orta - CustomerDisplay.tsx:97-104. Hub withAutomaticReconnect [0,1,3,5 sn] sonrasi kapanir; onclose/onreconnected yok, hub yeniden kurulmaz (performans ajani konusu). Yoklama 5 sn'de kurtarir, canlilik sessizce 5 sn gecikmeye duser.
F5 Orta - CustomerDisplay.tsx:54-55; DualScreenStore.cs:19 (12 saat). Mutlak oturum bitince snapshot 401 -> ekran musteri onunde aniden eslestirme koduna duser; kasada uyari yok; acik siparis tutari kaybolur. Oneri: sure dolmadan kayan yenileme (sunucu) veya kasada "ekran baglantisi koptu" gostergesi.
F6 Orta - ApiErrorCatalog.cs:579-580; CustomerDisplay.tsx:78. DualScreenConflictException "CONCURRENT_MODIFICATION / Kayit baska bir islem tarafindan degistirildi" uretir; eslestirmede bu "onaylanmadi/suresi doldu/kullanildi" demek. Kasada onay yanlis/suresi dolmus kodda 404 "Istenen kayit bulunamadi." doner; kasiyer yanlis kod mu sure mi anlayamaz (2 dk TTL, DualScreenStore.cs:18). Oneri: eslestirmeye ozel Turkce kodlar.
F7 Orta (kismen dogrulanamadi) - Cashier.tsx:278-307,726-783. Onay basarisizsa diyalog acik kalir, hata `error` durumuna yazilir; diyalog icinde render edilip edilmedigi okunmadi (aria-modal arka planda kalabilir).
F8 Orta - CustomerDisplay.test.tsx:68-84,18-33. Eslestirme icin yalniz mutlu yol testi. Sure dolma yenilemesi, 409 yoklamasi, revoke, hata ekrani, cevrimdisi/10 sn bayatlama, 12 saat dolumu testi yok; SignalR mock'unda onclose/onreconnected yok (F4 gorunmez). Bu dilimde sunucunun uretmedigi kodu taklit eden test bulunamadi: CONCURRENT_MODIFICATION yalniz billingApi/kitchenApi/tableApi testlerinde, ve sunucu o kodu gercekten uretiyor (ApiErrorCatalog.cs:580).
F9 Yuksek - help-request.js:45-60; HelpRequestExperience.cs (hub.Clients.Group(...).SendAsync sonrasi Results.Ok). Grupta kimse bagli olmasa da 200 doner; garsona her durumda "Yardim cagriniz yoneticiye iletildi." gosterilir. Teslim/okundu bilgisi ve kalici liste yok (Cashier.tsx:168 "no endpoint listing"). Etki: cagri kaybolur, garson yardim geliyor sanir. Oneri: bagli alici sayisi/kabul (acknowledge) ve "kimse bagli degil" uyarisi.
F10 Dusuk - help-request.js:54-56; api.js:29. 429 cooldown rutin deniyor ama sayfa acik kalip Cagir tekrar etkinlesir; ag yoksa cagri kuyruga alinmaz.
F11 Orta - waiter-app.js:147,686; push.js:56-136. Push yalniz Profil sayfasinda gizli bir dugmeyle acilir ve her cikista abonelik silinir (unsubscribePush); sonraki garson elle yeniden acmali, girisde oneri yok. Etki: cogu garson push almaz; ogrenme esigi.
F12 Dusuk - push.js:88-91. 'denied' ile 'default' ayirt edilmiyor; reddedilmisse tarayici ayarina yonlendirme yok. Cift dokunusa karsi koruma yok (enablePush eszamanli calisir).
F13 Dusuk - waiter-app.js:147-153. signOut once abonelik siler; cikis basarisiz olursa oturum acik kalip push sessizce kapanir.
F14 Orta - waiter-app.js:806-812; WaiterOrderStatusHub.cs:82 (payload'da TableId var); KitchenOperationsStore.cs:318. "OrderItemReady" tum cihazlara gider ve toast yalniz "<urun> hazir." der; masa numarasi yok (push govdesi de yok). Etki: garson hangi masa oldugunu bilmez, baskasinin urunune de uyari alir.
F15 Dusuk - sw.js:109-121. notificationclick acik pencereyi odaklar, data.url yok sayilir.
F16 Orta (E8: grep -rlE "help-requests|enablePush|push\.js|pushManager|notificationclick" tests/Clients -> 0 sonuc) - WaiterPwa yardim cagrisi, push akisi ve sw.js icin test yok; WaiterPwa icin axe taramasi yok; Cashier.help-alerts.test.tsx'te axe yok (CustomerDisplay ve Screensaver testlerinde var).
F17 Dusuk (Calistirildi: oxlint exit 0, 3 uyari) - Cashier.tsx:727,730. Eslestirme diyalogu backdrop'unda etkilesimsiz div uzerinde fare isleyicisi (jsx-a11y).
F18 Dusuk - CustomerDisplay.tsx:195. "Kod iki dakika sonra yenilenir" sabit metin; sunucu TTL'ye (DualScreenStore.cs:18) bagli degil, su an uyumlu.
Park edilmis V1-RMD-474/519/572/588 ile ortusen bulgu dogrulanmadi.

## 3. Temiz bulunan alanlar
- Ham hata: api.ts:101-123 ve waiter api.js:29-57 ag hatasini Turkce sarar; sunucu mesajlari Turkce; CustomerDisplay yalniz ApiError.message gosterir.
- Effect temizligi: CustomerDisplay zamanlayici/aralik/hub/objectURL (:84,91,105-108,166-169); Cashier help hub (:229-234) disposed + backoff.
- Pairing yoklama hizi (30/dk) sunucu limitlerinin (pairing-complete 60, display-read 240) altinda; 401 -> yeni kod ve sure dolunca yenileme dogru.
- Hub kimlik dogrulamasi sunucuda (CustomerDisplayHub.OnConnectedAsync).
- Musteri ekraninda para tr-TR (formatMoney), aktif ekran axe testi var.
- Push: HTTPS/iOS/izin/zaman asimi korumasi; sunucuya kaydedilemeyen abonelik tarayicidan geri aliniyor (push.js:124-130).

## 4. Dogrulanamayanlar
- F7 hatanin diyalog icinde gorunup gorunmedigi.
- Gercek tarayicida push/izin akisi ve SignalR 9 sn sonrasi davranisi (calistirilmadi).
- vitest kosulmadi; jsdom testlerinin gectigi dogrulanmadi.
- tsc/oxlint ciktilari: tmp/qa/V1-RMD-599/qa-frontend/tsc.txt, oxlint.txt.

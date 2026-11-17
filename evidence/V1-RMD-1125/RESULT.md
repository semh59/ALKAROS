# V1-RMD-1125 sonuc
Dogrulama: (1) Cashier.tsx:266 oturum bitis isleyicisi 401'de oturumu "anonymous" yapip Son satislar ekranini sokuyor ve girise "Oturumunuz sonlandirildi" koyuyor; oturum acik degilken ekran zaten yok: "Oturum suresi doldu, yeniden giris yapin." cumlesi erisilemezdi. (2) Band (workspace.tsx CashSessionBand) ve sayfa mesaji kasa kapaliyken ayni seyi soyluyordu: gercek.
Duzeltme: recentSalesText.sessionExpired ve 401 dali kaldirildi (notifySessionExpired cagrisi ve oturum sonlandirma davranisi ayni); band "closed" iken sayfa mesaji gosterilmez (closedBandShown), band yoksa sayfa mesaji kalir.
Kanit: red.log 3 kirmizi; green.log 3 dosya/95 test yesil; green-full.log 125 dosya/1496 test yesil; client-build-gate exit 0; mutations.txt M1-M5 killed (cp+cmp).
Acik risk: yok bilinen; band yalniz state=closed iken sayfa mesajini bastirir (notOwned/unknown icin sayfa mesaji eskisi gibi).

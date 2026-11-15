# V1-RMD-1113 sonuc
Dogrulama (kod): .kitchen-order__stations (kitchen-operations.css) yukseklik siniri tasimiyordu; her kalemin 48 px adim dugmeleri sarilarak kart sinirsiz uzar. Alarm gercek.
Duzeltme: yalniz CSS: .kitchen-order__stations { max-height: min(36rem, 70vh); overflow-y: auto }. Baslik (masa, sure, uyari dugmesi) bu alanin disinda, her zaman gorunur; 14 px yazi ve 48 px dugme kurallari degismedi (kitchenTextSize testi yesil).
Kanit: red.log (siniri arayan test kirmizi), green.log mutfak klasoru 7 dosya/111 test yesil, mutations.txt M1-M4 killed, client-build-gate.log exit 0.
Acik risk: sinirli alan dikey kayar; cok biletli kartta alttaki bilet kaydirmadan gorunmez (dokunmatik kaydirma). Gercek tarayicida yukseklik olculmedi (jsdom CSS yerlesimi hesaplamaz); 1113 ardindan canli denemede dogrulanmali. 1112 ile kapali hesap biletleri zaten panodan duser.
Ek acik risk (qa-frontend, Orta): kaydirilabilir alanda odaklanabilir dugme yoksa (yetkisiz/Served) klavye ile kaydirilamaz; cozum KitchenOperationsWorkspace.tsx'e tabIndex/aria-label, bu gorevin Owned surface'i disinda (ayri gorev). Dusuk: 70vh mobil adres cubugu, kaydirma ipucu yok.

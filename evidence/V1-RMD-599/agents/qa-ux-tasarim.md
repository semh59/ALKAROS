# V1-RMD-599 Host (dar kapsam) - UX/tasarim denetimi (qa-ux-tasarim)
SHA d1dc86b6 (git rev-parse --short HEAD). Tum dosya:satir bu commit e gore. Kanit duzeyi hepsi "Kod okumasi" (Playwright kosusu YAPILMADI, bkz. 6).

## 1. Kapsam envanteri
M=44 dosya kapsamda: src/Host/DualScreen/* (18), Experience/HelpRequests (4), WebPush (4), WaiterNotifications (5), Orders/CashierQueueAnnouncer.cs (1); istemci: PosTerminal routes/CustomerDisplay.tsx, CustomerDisplayScreensaverSettings.tsx, routes/Cashier.tsx, features/pending-checks/PendingChecksWorkspace.tsx, online-operations/useNewOrderChime.ts, api.ts, styles.css; WaiterPwa js/push.js, js/sheets/help-request.js, js/auth.js, js/kiosk-lock.js, js/api.js. (Glob/grep ile elle listelendi.)
Tam okunan: CustomerDisplay.tsx, Cashier.tsx, DualScreenStore.Display.cs, Endpoints.cs (45-365, 485-545), HelpRequestExperience/Hub, push.js, help-request.js, kiosk-lock.js (136-250), PendingChecksWorkspace.tsx (1-140), SignalRCashierQueueAnnouncer.cs, CustomerDisplayHub.cs. Kismi/grep: ApiErrorCatalog, DualScreenApplication.cs, styles.css, api.ts, useNewOrderChime. Okunmayan: WebPush sunucu dosyalari, HelpRequestStore/Contracts, WaiterNotifications hub/presence, ScreensaverSettings, strings.ts.

## 2. Semih senaryosu: Musteri ekrani eslestirilir; urun eklenince aninda gorunur; eslestirme iptal edilince ekran veri gostermez
Calisan adimlari:
1. Kasada "Musteri ekrani" dugmesi -> yeni sekme /display (Cashier.tsx:454-459). 1 dokunus.
2. Ekran 8 haneli kodu gosterir (CustomerDisplay.tsx:193-195); kod 2 dk gecerli, dolunca kendini yeniler (DualScreenStore.cs:18, CustomerDisplay.tsx:87-92).
3. Kasaya don -> "Ekrani eslestir" (Cashier.tsx:481) -> 8 karakter yaz -> "Ekrani eslestir" (763-767). Toplam yaklasik 5 dokunus + 8 tus + pencere degistirme.
4. Ekran 2 sn yoklamayla tamamlar (CustomerDisplay.tsx:71-83), 12 saatlik oturum alir (DualScreenStore.cs:19).
Urun ekleme: kasada urun karti 1 dokunus -> addItem -> sunucu NotifyAsync (Endpoints.cs:430) -> hub SnapshotChanged -> ekran aninda yeniler (CustomerDisplay.tsx:102). Hub koparsa 5 sn yoklama (96); 10 sn sonra veri silinir, "Bilgi guncellenemiyor" (111-119, 375). Yani anlik, en kotu durumda <=5 sn.
Iptal: "Ekrani eslestir" penceresi -> "Aktif ekran yetkisini kaldir" (Cashier.tsx:772-779) = 2 dokunus, onaysiz. Uc nokta display-sessions/revoke (Endpoints.cs:535-544) oturumlari iptal eder ama hub a bildirim ATMAZ (NotifyAsync 6 cagri, revoke da yok). Ekran sonraki yoklamada (<=5 sn) 401 alir -> beginPairing (CustomerDisplay.tsx:54-55) -> eslestirme ekrani; eski urunler gosterilmez (ayri render dali). Backend ajaninin <=5 sn bulgusu kodla dogrulandi.
Degerlendirme: senaryo calisiyor; ama eslestirme adim yuku yuksek (B-8), iptalde onay/geri bildirim zayif (B-3, B-4), iptal sonrasi ekran musteriye "kasaya baglayin" + kod gosterir (B-9).

## 3. Bulgu tablosu (kanit duzeyi: Kod okumasi, aksi belirtilmedikce)
| No | Onem | Kanit | Sorun | Etki | Oneri |
|---|---|---|---|---|---|
| B-10 | Yuksek | HelpRequestExperience.cs:99-104 hub grup SendAsync sonra Results.Ok (alici sayisi yok); help-request.js:57 toast "Yardim cagriniz yoneticiye iletildi."; help-request.js:37 "nobetci yoneticiye aninda bildirim gider" | Bagli yonetici yokken sunucu yine 200 doner; garson "iletildi" gorur, cagri kaybolur. Cagri kalici degil, liste ucu yok (Cashier.tsx:168-170 kendisi yaziyor). Ayni masada 2 dk bekleme | Dokuntu/sikayet cagrisi kimseye ulasmaz, garson bilmez | Alici sayisini yanitla; 0 ise "Su an bagli yonetici yok, kasaya haber verin"; cagriyi kalici kil, "goruldu" geri bildirimi ver |
| B-1 | Orta | DualScreenStore.Display.cs:90 DualScreenNotFoundException (Ingilizce metin) -> ApiErrorCatalog.cs:579 "Istenen kayit bulunamadi."; alfabede I,O,0,1 yok (DualScreenStore.cs:16) ama Cashier.tsx:751 buna izin verir -> ArgumentException -> "Istek dogrulanamadi." | Yanlis/suresi dolmus kodda kasiyer ne yapacagini ogrenmez | Kasiyer tekrar dener, kod arada yenilenmis olabilir | Ozel Turkce mesaj: "Kod hatali veya suresi doldu; ekrandaki yeni kodu girin"; girdide 0/O/1/I reddet |
| B-2 | Orta | Cashier.tsx:763-767; pairingCode yalniz 67 ve 751 de ayarlanir (temizleme yok) | Basarida pairingCode temizlenmiyor; pencere yeniden acilinca kullanilmis kod dolu, dugme etkin | Tekrar gonderim, B-1 hatasi | Basarida alani sifirla |
| B-3 | Orta | Cashier.tsx:772-779 | Yikici "Aktif ekran yetkisini kaldir" onaysiz; musteri onundeki ekran <=5 sn de koda doner | Yanlis dokunus canli satista ekrani dusurur; geri alma ~5 dokunus | Kisa onay veya Geri al |
| B-4 | Dusuk | api.ts:265-269 revoked sayisi doner; Cashier.tsx:777 sabit "Ekran yetkisi kaldirildi." | revoked=0 iken de basari mesaji | Yanlis guven | 0 ise "Bagli ekran yok" |
| B-5 | Orta | CustomerDisplay.tsx:193 "...kasa ekranindaki Musteri ekrani baglantisi alanina girin"; kasada etiketler Cashier.tsx:481 "Ekrani eslestir", 742 "Ekran baglantisini yonetin" | Ekrandaki talimat kasada olmayan etiketi soyluyor | Ogrenme esigi: calisan alani bulamaz | Etiketleri esitle |
| B-6 | Orta | CustomerDisplay.tsx:37,57,80 reason.message; api.ts:~120 sunucu govdesi mesaji; pairing-create 10/dk (DualScreenApplication.cs:371-372) | Ham sunucu mesaji basiliyor (UI_STYLE_GUIDE: sunucu govdesi gosterilmez). Koda ozel mesaj yok; 429 yon vermeyen genel metne duser | Musteri ekraninda teknik/yanlis metin | error.code -> sabit Turkce sozluk |
| B-7 | Orta | DualScreenStore.cs:19 (12 sa) vs login 8 sa (Endpoints.cs:78); CustomerDisplay.tsx:54-55 | Ekran 12 saat sonra otomatik koda duser; kasiyere onceden uyari yok; ekran ne yapilacagini yanlis etiketle soyler (B-5) | Yogun saatte musteri onunde kod ekrani | Kasada "Ekran bagli/degil" rozeti, dolmadan yenileme veya kullandikca uzayan oturum |
| B-8 | Orta | Cashier.tsx:454-482 | Sekme ac, kodu oku, pencereye don, ac, 8 karakter yaz. Sistem ayni tarayicida displayId/terminalId yi zaten bilir | ~5 dokunus + 8 tus, hata payi | Ayni cihazda tek dokunusla otomatik eslestirme veya QR |
| B-9 | Dusuk | CustomerDisplay.tsx:190-195 | Kod musteriye bakan ekranda acik; iptal sonrasi hemen gosterilir | Musteri onunde teknik ekran | Marka ekrani + kucuk kod |
| B-11 | Orta | Cashier.tsx:154-235, 504-521; ses yalniz useNewOrderChime ta (Cashier.tsx:64) | Yardim uyarisi sessiz, kapatilinca kaybolur, "ilgileniyorum" yok; iki yonetici ayni cagriya gider | Gurultulu salonda kacar | Sesli uyari + ustlenme eylemi |
| B-12 | Orta | PendingChecksWorkspace.tsx:1-140; chime/audioAlerts aramasi bu klasorde 0 sonuc; REFRESH_MS 60_000 (satir 9) | Garsonun kasaya gonderdigi hesap kasada sessiz; hub kopmasi gosterilmiyor (Cashier daki yardim baglanti uyarisinin aksine); yukleme hatasinda eski liste "eski veri" etiketi olmadan kalir | Yogun saatte bekleyen hesap fark edilmez, liste 60 sn eski olabilir | Ses/rozet; "Canli guncelleme yok" seridi |
| B-13 | Dusuk | Cashier.tsx:368 "ALKAROS RESTAURANT OS"; Cashier.tsx:450 title "Gercek /health/ready sonucu" | Ingilizce metin ve teknik yol kullaniciya gorunur (UI_STYLE_GUIDE 4 sizinti) | Dil tutarsizligi | Turkce etiket, title kaldir |
| B-14 | Orta | push.js:61-72, 90-92, 108-110 | Izin reddinde yalniz "Bildirim izni verilmedi."; tarayici ayarindan nasil acilacagi yok, tarayici tekrar sormaz; abonelik hatasinda sebep ayrimi yok; key.message sunucu metni | Garson sessizce bildirimsiz kalir (yemek hazir uyarisi gelmez) | "Tarayici ayarlarindan bu site icin bildirime izin verin" + profilde durum rozeti |
| B-15 | Orta | kiosk-lock.js:16,144-145,181-186; Cashier.tsx icinde unlock aramasi 0 sonuc | Garson uygulamasi 3 dk bostaysa PIN kilidi; PosTerminal kasasinda bosta kilit yok; oturum 8 sa bitince yalniz mesaj (Cashier.tsx:142), acik siparis durumu hakkinda bilgi yok; 4 haneli PIN te de onay tusu zorunlu | Egitim gerektiren davranis; terk edilmis kasa | Kasaya kilit; PIN uzunlugu sabitse otomatik gonder |
| B-16 | Dusuk | styles.css:349-352 (44 px hedefler, bitisik); waiter-app.css:1382 pin-key 58 px | Islak/yagli el icin kucuk; Kaldir onaysiz/geri almasiz | Yanlis dokunus | Hedefleri 56+ px yap, Kaldir icin geri al |
| B-17 | Dusuk | Cashier.tsx:328,351 ham reason.message | B-6 ile ayni kok neden (login/logout) | - | Sozluk |
| B-18 | Orta (Cikarim) | DualScreenStore.Display.cs:253-255 yorum: modifier hesaba katilmiyor | Secenekli urunde musteri ekrani tutari kasa toplamindan farkli olabilir | Musteri itirazi | Calistirilmadi, dogrulanmali (park RMD-474/519/572/588 ile iliski kontrol edilmedi) |

Dokuman sapmasi: HelpRequestHub yorumu "kasiyer haric" der; Cashier.tsx:154-181 her oturum icin hub a baglanir (kasiyer-only oturum reddedilir). Tasarim geregi, karar verilmedi.

## 4. Firsat tablosu
| Akis | Simdi | Firsat |
|---|---|---|
| Musteri ekrani eslestirme | ~5 dokunus + 8 karakter, iki pencere | Ayni cihazda otomatik/QR eslestirme, sifir karakter |
| Eslestirme durumu | Kasada gorunmuyor | "Ekran bagli/degil" rozeti, 12 sa dolmadan hatirlatma |
| Eslestirme iptali | 2 dokunus, onaysiz, geri almasiz | Onay + Geri al |
| Yardim cagrisi | Garson 3 dokunus (dugme, tur, Cagir), sonuc garantisiz | Alici sayisi, "goruldu" geri bildirimi, varsayilan tur |
| Bekleyen hesap duyurusu | Sessiz liste | Ses/rozet; canli baglanti gostergesi |
| Push izni | Reddedilince yol gosterilmiyor | Ayar yonlendirme metni, durum rozeti |
| PIN/oturum | Garson 3 dk kilit; kasada yok | Kasaya kilit; otomatik acma |
| Hata mesajlari | Ham sunucu mesaji | error.code sozlugu |

## 5. Temiz bulunanlar
- CustomerDisplay.tsx sabit metinleri Turkce; eski veri 10 sn sonra silinir ve nedeni soylenir (111-119, 375); role alert/aria-live dogru.
- Kasada oturum bitis mesaji Turkce ve acik (Cashier.tsx:142,409); ag hatasi Ingilizce sizdirmaz (api.ts:101-105; waiter api.js:28-29).
- Yardim baglanti kopma/kacirma durumlari kasada acik (Cashier.tsx:490-503).
- PIN ekrani cevrimdisi/429/5xx ile hatali PIN i ayirir, girileni korur, 423 te sifre girisine yonlendirir (kiosk-lock.js:200-228).
- Urun ekleme -> musteri ekrani anlik (NotifyAsync Endpoints.cs:430) dogrulandi.

## 6. Dogrulanamayanlar
- Playwright ekran goruntusu alinmadi; kontrast, kucuk ekran duzeni, ikinci ekran davranisi dogrulanamadi. tmp/qa altinda goruntu yok.
- UI_STYLE_GUIDE 4 Ingilizce sizinti taramasi komutu calistirilmadi; B-13 dosya okumasindan.
- WebPush sunucu, HelpRequestStore, ScreensaverSettings, strings.ts okunmadi.
- B-18 yalniz yorumdan (Cikarim).
- Iptal uc noktasinin testi yok iddiasi (backend ajani) tarafimdan dogrulanmadi (tests aramasi zaman asimina ugradi).

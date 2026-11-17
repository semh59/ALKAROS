# V1-RMD-1129 sonuc
Dogrulama: gercekti; indirim/ikram beklerken kasiyer yalniz bekliyordu.
Duzeltme: split-payment.js - bekleyen talepte "Yonetici burada onaylasin" (kullanici adi + sifre, Enter ile de), V1-RMD-1128 ucu; basarida ayni talep ayni anahtarla yeniden gonderilir, bildirim "X onayiyla uygulandi". Sifre state/depolama/URL/log/DOM disinda; hata cumleleri ERROR_TEXTS sozlugunde sunucuyla ayni; onay ucustayken form duzenlenirse eski talep yeniden gonderilmez.
Kanit: red.log 10 kirmizi; green.log 64 dosya/719 test; M2-M11 killed (cp+cmp; M1 esdeger mutant olarak atildi: render alani zaten bosaltir). client-build-gate: istemci (PosTerminal) degismedi, derlenecek bir sey yok.
Acik risk (dusuk): onay ucustayken duzenle+yeni talep durumunda bildirimde yanlis yonetici adi; kullanici adi alanina sifre yazilirsa denetim kaydina duser; gorunur etiket yerine placeholder+aria-label.

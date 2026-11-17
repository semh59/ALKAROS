# V1-RMD-1128 sonuc
Dogrulama: gercekti; kasiyer indirim/ikram talebi yalniz yonetim ekranindan onaylanabiliyordu. Mevcut altyapi: AuthenticationService (sifre, kilit, sahte hash), AuthorizationDecisionStore.ApproveAsync (4-goz, yetki, ayni islemde denetim). PIN baska kullaniciyi dogrulamaz, bu yuzden sifre kullanildi.
Duzeltme: POST .../billing/bills/{billId}/approvals/{grantId}/approve; talep bu hesap+bu kasiyer+kendi anahtari ile baglanir; yonetici sifresi + reports.view + talep izni; kasiyer ve yonetici basina 3 hatali deneme (bellekte) yonetici kilidinden (5) once 429; her basarisiz deneme denetim izine; sifre log/yanit/ToString disinda.
Kanit: red.log 7 kirmizi; green.log 15/15; sinif tam kosusu 83/83 (ilk surum); ApiConventions 17/17; M1-M13 killed (cp+cmp).
Acik risk: sayac bellekte (yeniden baslatmada sifirlanir); IP login siniri ve SuspiciousLogin sarmalayicisi kullanilmiyor; reports.view eksikligi ile yanlis sifre ayirt edilebilir (dusuk).

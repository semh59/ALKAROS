# V1-RMD-1047 RESULT

Karar (Semih 2026-11-13): yonetici onayli ama fisi/havalesi girilmemis iadeyi iptal edebilsin; kasiyer goremez.
Duzeltme: kasa (split-payment) ekraninda "Elle iade bekliyor" satirinda (Kart ve Havale), payments.refund.approve yetkisi olan ve karar verebilen (mayDecide) kullaniciya gerekce + iki dokunuslu "Onayli iadeyi iptal et" gorunur; mevcut /reject ucu cagrilir (V1-RMD-1045). Basarida "Onayli iade iptal edildi; para iade edilmedi. Hesap yeniden kapatilabilir." Karar veremeyene "Baska bir yetkili onaylamali." Yetki bayragi canResetReceipt -> canApproveRefund yeniden adlandirildi (3 yer, ayni anlam: payments.refund.approve; kalan kullanim yok).
Gerekce secimi/yazimi degisince ikinci dokunus sorusu duser; onay sonrasi reddetme gerekcesi iptal alanina tasinmaz.
Kanit: red.log (4 yeni test kirmizi), green.log StaticApps 47 dosya tum suite, mutations.txt M1-M11 killed, task-scope exit 0. client-build-gate: PosTerminal degismedi, derleme gerekmedi (exit 0).
Elle senaryo: kartli odeme, iade iste, yonetici onayla; kasa ekraninda yonetici hesabiyla "Onayli iadeyi iptal et": gerekce sec, iki dokun; iade Reddedildi, hesap kapanabilir. Kasiyer hesabinda dugme yok.
qa-frontend: yetki/anahtar/yeniden adlandirma temiz. Dusuk: eski gerekce tasiniyor -> DUZELTILDI (M10); serbest metin degisince soru dusmuyor -> DUZELTILDI (M11). Cift dokunus testi yok (mevcut kalip, busy korumasi ortak).
qa-test-kalitesi: kabul kosullari kilitli, hayatta kalan mutant yok. Dusuk: Requested/Rejected satirinda yok testi (yapi geregi yalniz Awaiting dallarindan cagrilir, kabul).
Dogrulama turu: duzeltmelerden sonra tum suite yesil, M1-M11 killed.

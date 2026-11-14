# V1-RMD-1087 sonuc
Dogrulama (kod): cash-tender ucu (DualScreenApplication.CashSession.cs:282) oturum sahibi cagiran degilse yetkiden bagimsiz 409 CASH_SESSION_NOT_OWNED doner; yonetici muafiyeti yok. Uretim kodu degismedi. Kirmizi test yok: kural V1-RMD-1049'dan beri vardi, eksik olan yonetici testiydi.
Eklenen: CashTenderByAManagerIntoAnotherUsersOpenSessionIsRefusedLikeACashier (cash.session.override'li yonetici baskasinin oturumuna -> 409 + kod + odeme yazilmaz), CashTenderByAManagerIntoTheirOwnOpenSessionSucceeds (kendi oturumu 200).
Kanit: green-test.log 3/3, green-class.log CashSessionHttpTests 32/32, mutations.txt M1 (kontrol silindi) ve M2 (yonetici muafiyeti eklendi) oldu, cmp ok.
Acik risk: cash-movements ucu (para giris/cikis) oturum sahibi kontrolu yapmiyor; kapsam disi, karar gerekir.
Not (qa): account-receipts ucundaki ayni acik V1-RMD-1088'de kapatilir.

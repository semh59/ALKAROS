# V1-RMD-1064 RESULT

Baslikta acik oturum giris yapanin degilse rozet "Kasa: Baska kullanicida" yazar ve calisma alaninin ustunde sarit "Oturumu acan kasiyer ya da bir yonetici kapatmali, sonra kendi oturumunuzu acin" der (Kasa oturumunu gor baglantisi). Sahiplik, /active yanitindaki cashierUserId ile giris yapanin userId'sinin karsilastirilmasidir (hook'a userId parametresi eklendi, Cashier.tsx reload'da set eder). Tahsilat sayfasi "ör. yönetici, önce kapatmalı" yerine "oturumu açan kasiyer ya da bir yönetici kapatmalı" der.
Kanit: red.log, red-split.log, green-file.log, green-all.log (PosTerminal PosTerminal tum suite), green-static.log (StaticApps 50 / 586), client-build-gate.log exit 0, mutations.txt (14 killed).
Ekranda dogrulanmadi: gercek tarayicida gorsel.

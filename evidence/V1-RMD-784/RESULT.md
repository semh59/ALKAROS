# V1-RMD-784 sonuc

Dogrulanan ve duzeltilenler: ust cubuk sarma (929/1024 px E2E kirmizi->yesil), split-payment ve cash-session "Kasaya don", kat plani 404 tekrar istegi (bilinen plansiz bolge sessiz yenilemede sorulmaz), ISO saat -> Turkce saat (mutfak, katalog, sistem sagligi), ondalik virgul, "Mutfak ekrani" basligi, surum/v etiketleri, "Garson bildirimi kapali" rozeti, /billing "Aktif adisyon yok", 48 px dokunma hedefleri.
Zaten duzgun / dogrulanamayan: customer-accounts "Kasaya don" vardi; masa karti secimi paneli dogru guncelliyor (hizli islem bilerek bagimsiz); "Masayi ac" ve birlestirme listesindeki Musait masalar sunucu kurali (degistirilmedi).
Kapsam disi birakilan: musteri ekrani (kismi odeme, /cashier siparisi) ayri gorev gerektirir; canli imaj yenileme yapilmadi.
Testler: PosTerminal vitest 834/834, StaticApps 144/144, tsc 0, lint 0 hata, E2E 78/80.
E2E 2 basarisiz: spec 11 (masa devri) "TRF-2 not offered": spec secenek metnini "TRF-2 " (bosluklu) bekliyor, TableWorkspace secenekleri yalniz masa numarasi yaziyor (V1-RMD-713'ten kalma, bu gorevde dokunulmadi) -> onceden var olan hata.
Mutasyon: mutation.log (11 gecerli mutant testleri kirdi, cmp ile geri alindi; ilk NOCHANGE satirlari gecersiz denemeydi, gecerli olanlari tekrar edildi).
Not: kapanis kapisi V1-RMD-604'u (baska oturumdan InProgress) gosterdi; dokunulmadi, override kullanildi.
Incelemeler: qa-frontend (Kritik/Yuksek yok) ve qa-test-kalitesi sirayla yapildi; /billing rota testi ve ek mutantlar eklendi. Kabul edilen kalan riskler: saat yalniz saat gosterir (tarih yok); iki "Kasaya don" farkli hedefe gider (odeme sayfasi ana kasa, kasa oturumu hizli kasa - cikis oldugu sayfaya gore).

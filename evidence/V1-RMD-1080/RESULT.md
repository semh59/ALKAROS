# V1-RMD-1080 RESULT
Uretim kodu degismedi; 2 yeni test: (a) suresi dolmus oturum ve baska terminal oturumu 401, musteri ekrani oturumu 403; (b) LIMIT 500: en yeni 500 is doner, en eski dusuyor.
Test: yeni 3/3 (green-new.log), KitchenOperations TAM kosu 78/78, 5 dk 16 sn (green-full.log, bolmeye gerek olmadi). 4 mutant killed (LIMIT yok, ASC, yetki yok, 403->401), cmp ok. Mutant oncesi kirmizi: testler mevcut davranisi sabitler, kirmizi kanit mutasyondur.
LIMIT 500 gerekcesi: bilet basina normalde tek is (dagitici yalniz isi olmayan bileti isler), istasyonda 500+ basarisiz is pratikte beklenmez; kesilirse en yeni isler kalir. Log/kullanici bildirimi eklenmedi. Istenirse LIMIT 501 ile asim uyarisi ayri gorev (Dusuk).
SQL Host'ta (kitchen.print_jobs, modul deposu disinda): store'da zaten ham SQL var (catalog, identity, orders); fark kendi modul tablosunu depoyu atlayarak okumasi ve sutun eslemesinin PostgresPrintQueueRepository ile cogalmasi. Guvenlik/dogruluk etkisi yok, tasinmadi.
Not: SeedSessionAsync(expired: true) chk_device_sessions_lifetime yuzunden kirik; test suresini UPDATE ile doldurur (yardimci sinif kapsam disi).

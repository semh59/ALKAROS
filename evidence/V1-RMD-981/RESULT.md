# V1-RMD-981 sonuc

Kok neden: kilitlenme yoktu. DualScreenStoreTests her testte yeni veritabani olusturup 230 migrationi bastan calistiriyordu (test basi ~12 sn); 41 testlik sinif 8,6 dakika surdu (class-run.log) ve kisa zaman asimli kosular "test ana islemi kilitlendi" ile kesildi. Billing test projesi de ayni nedenle 15,7 dakika (V1-RMD-980/billing2.log). Tek tek hicbir test yavas degildi.
Duzeltme: TestDatabase.InitializeMigratedAsync: migrate edilmis sablon veritabani surec basina bir kez kurulur, her test CREATE DATABASE ... TEMPLATE ile kopyalar; sablon surec cikisinda silinir. DualScreenStoreTests bunu kullanir.
Sonuc: 41 test gecti, 48-69 sn (green.log); once 8,6 dk. Sablon sureç cikisinda siliniyor (kosu sonrasi alkaros_tpl_* kalmadi).
Acik: ayni deseni kullanan diger test siniflari (Billing, TableDraft vb. kendi TestDatabase siniflari) henuz sablona gecmedi; test PG kapsayicisinda onceki durdurulan kosulardan kalan yaklasik 380 veritabani var (alkaros-test-pg temizligi Semih onayi ister, tehlikeli komut freni).

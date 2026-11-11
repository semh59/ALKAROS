# V1-RMD-980 incelemeleri

## 1. qa-backend
Bulgular: (Yuksek) kilit sonrasi siparis durumu (Cancelled/Rejected/Draft) tekrar kontrol edilmiyor; (Yuksek) tablosuz siparis dali kilitsiz; (Orta) yeniden okuma ayri baglantidan; (Dusuk) null kontrolu. Kilit sirasi: ters sira / kilitlenme riski bulunmadi.
Aksiyon: kilit ve yeniden okuma her iki dalda (dataSource varsa), yeniden okuma islem ici (PostgresOrderRepository.GetByIdAsync(conn, tx)), durum kontrolu kilit sonrasi tekrarlandi. Testler: masali ve masasiz kosu (Theory), iptal edilmis siparis testi. Mutantlar M3 (durum kontrolu yok) ve M4 (yalniz masali) oldu.

## 2. qa-test-kalitesi
Bulgular: (Orta) Task.Delay(1500) kirilgan; (Orta) mutant seti dar; (Orta) yetersiz stok testi hata kodunu dogrulamiyor; (Orta) masasiz dal testsiz; (Dusuk) kilit sonrasi iptal testi yok.
Aksiyon: sabit bekleme yerine pg_stat_activity ile kilit bekleyeni bekleniyor (en fazla 10 sn) ve yanit 30 sn ile sinirli; INSUFFICIENT_STOCK kodu dogrulaniyor; masasiz ve iptal testleri eklendi; M3/M4 mutantlari eklendi.
Acik: yeterli stokla sinir degeri (tam yetecek stok) denenmedi.

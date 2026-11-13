# V1-RMD-1051 RESULT

Kok neden: masa paneli okunan siparis kalemlerini state'te tutuyordu; masa bosalinca (siparis kimligi kalkinca) okuma yapilmadigi icin eski kalemler "Mutfaga gondermeden hesaba ekle" ve "Mutfaga gonder (n)" dugmelerini besledi.
Duzeltme: okunan kalem listesi okundugu siparise baglandi; masanin guncel siparis kimligiyle eslesmeyen, kalem okuma yetkisi olmayan ya da kimligi kalkmis okuma kullanilmaz (render aninda turetilir, ara kare yok). Bos masa yalniz Siparis al gosterir.
Kanit: red.log (bayat dugme), green-file.log, green-all.log PosTerminal 116 dosya/1342 test, client-build-gate.log exit 0 (oxlint --max-warnings=0, tsc, vite build), mutations.txt M1-M5 killed.
Elle senaryo: siparisli masayi baska masaya tasi; bosalan masanin paneli Yenile'siz yalniz "Siparis al" ve "Bu masada acik hesap yok" gosterir.
Urun karari (listelenir): sessiz arka plan okuma hatasinda panel "guncel degil" ibaresi gostermiyor; hesap kapaninca panel yalniz "acik hesap yok" diyor.

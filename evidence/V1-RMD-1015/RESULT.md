# V1-RMD-1015 - Masalar ekranina donunce son secilen masa secili kalir

Kok neden: workspace.tsx secili masa durumu her acilista null basliyor, ilk yuklemede ilk masa seciliyordu.
Duzeltme: secim "alkaros.selected-table" anahtarinda saklanir ve baslangicta okunur; masa listede yoksa mevcut yedek davranis ilk masayi secer.
Kanit: yeni test once kirmizi; routes 32 dosya 313/313; client-build-gate exit 0; gercek tarayici: M-11 secildi, Hizli satis'a gidip Masalar'a donunce M-11 secili (masalara-donus-secili-masa.png). Mutasyon 2/2 killed.
Qa sonrasi: Diger menusundeki Cikis hatirlanan masayi siler (sonraki kasiyer eski masayla acmasin); test + mutant 3/3 killed. Satis ekranindaki Cikis (Cashier.tsx) anahtari henuz silmiyor: V1-RMD-1016'da.

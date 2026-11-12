# V1-RMD-1014 - Acik hesapli dolu masada Musait yap gosterilmez

Tekrar: gercek ekranda M-02 (Odeme bekliyor, hesapli) secilince Diger islemler menusunde "Musait yap" cikiyordu.
Duzeltme: models.ts hasOpenCheck + isOfferedAction; masa karti hizli islemi, masa paneli Diger islemler menusu ve plan gorunumu denetcisi acik siparis/hesabi olan masada SetAvailable sunmaz (hesapsiz dolu masada sunulur).
Kanit: eski beklenti testi guncellendi (once kirmizi), 3 yeni test; tables+routes 37 dosya 389/389; client-build-gate exit 0; gercek tarayici: M-02 Diger islemler yalniz "Masalari birlestir" (dolu-masa-diger-islemler.png). Mutasyon 3/3 killed.
Not: sunucu Occupied durumunda SetAvailable'i hala izinli listeler (TableManagementContracts.cs:291); sunucu tarafi kural bu gorevin disinda, acik risk olarak raporlandi.
Qa sonrasi: 78/78 tables testi, client-build-gate exit 0.

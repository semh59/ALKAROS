# V1-RMD-1006 - Masa planinda tek sag panel

Sonuc: secili masa tek kutuda; masa adi, durum, kalemler, toplam, ana eylem (dolu masada Ode, bos masada Siparis al), Urun ekle, Mutfaga gonder (n), Mutfaga gondermeden hesaba ekle, Bol / Masa tasi / Iptal-iade. Masayi ac, Rezervasyon, Birlestir, Servis disi gibi eylemler Diger islemler menusunde (kapali).

- Mutfaga gonder (n): yalniz gonderilmemis (Draft) kalem adedi; panelden tek dokunusla siparisi mutfaga gonderir ve masalari yeniler. Satis ekraninda da ayni sayim (kasa icin Unsent bayragi, musteri ekranina gitmez).
- Mutfaga gondermeden hesaba ekle: panelden satis ekranini sebep secici acik halde acar.
- Gercek ekran goruntuleri (vite preview, Playwright, tek sekme): panel-1024x768.png, panel-1024x768-kaydirilmis.png, panel-1366x768.png, panel-768x1024.png.
- Istemci: vitest 1292/1292, oxlint 0, tsc 0, vite build 0 (client-all.log, lint.log, tsc.log, build.log). Backend: DualScreenStoreTests 44/44 (backend-dualscreen.log).
- Mutasyon: 16 mutasyon, 16 killed (mutations.txt).
- Acik not: Bol ve Iptal / iade ayni faturalama ekranina gider; harita gorunumunde panel kendi kutusunda kalir.

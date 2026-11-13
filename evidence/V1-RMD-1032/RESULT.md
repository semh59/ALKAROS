# V1-RMD-1032 - Masa panelinin eylemleri 1024x768 ve 768x1024 ilk ekranda

Duzeltme: TableOrderBridge.tsx eylem dugmeleri (Ode, Urun ekle, Bol, Masa tasi, Iptal / iade), neden ve hata mesajlari kalem listesinin ustune alindi; tables.css 768-820 px arasinda panel masalarin yaninda kalir (tek sutun yalniz <=767 px).
Gercek ekran: Chromium (Playwright), bu gorevin build'i vite preview ile, API page.route ile taklit; getBoundingClientRect: onceki 1024x768 Bol y=891 (ekran 768), 768x1024 Ode y=1244; sonra 1024x768 Bol 689-751, 768x1024 Bol 758-820, 1366x768 671-719; yatay tasma yok (browser-measure.txt, after-*.png).
Kanit: yeni test once kirmizi; tables/shell/workspace/TableOrderBridge 191/191; client-build-gate exit 0; mutasyon 3/3 killed.
Acik: "Toplam" satiri artik dugmelerin altinda; arac cubugunun 768 px'te kesilmesi taklit verisiyle gorulmedi (gercek veride dogrulanmadi).

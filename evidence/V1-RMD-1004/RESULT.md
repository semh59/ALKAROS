# V1-RMD-1004 sonuc

Yapilandirma dugmeleri (Bolge ekle, Masa ekle, Masayi duzenle, Bolgeleri yonet, Plani duzenle) yalniz floorplan.manage yetkisine bagli (routes/workspace.tsx canConfigure); sunucu ayni yetkiyi ister. Masa tanimsizken yetkisiz kullanici "Mudurunuze bildirin" gorur. Testler client-all.log, mutations.txt (1 esdeger mutant).
Acik: kasiyer/garsonun onceki canManage ile plan duzenleme yetkisi artik yok (kasiyer rolu floorplan.manage tasimiyor; sunucu zaten reddederdi).

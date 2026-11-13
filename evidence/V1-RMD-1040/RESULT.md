# V1-RMD-1040 RESULT

Sorun: dugme "Masa tasi", pencere basligi ve bildirim "Masa degistir"; kasiyer giris basligi "Kasayi ac" (kasa oturumu acmayla karisir).
Duzeltme: strings.ts tableActionLabels.Transfer = "Masa tasi" (dugme, dialog basligi, "Masa tasi tamamlandi." bildirimi, salon plani dugmesi tek kaynaktan); TableWorkspace bos hedef cumlesi "masa tasinamaz"; Cashier.tsx giris basligi "Giris yap" (dugme ve diger giris ekranlariyla ayni kok). Kalan "Kasayi ac" yalniz kasa oturumu baglantilari (dogru anlam). E2E spec dugme adi guncellendi.
Kanit: red.log 13 test kirmizi; green PosTerminal tum suite 116 dosya 1328/1328; mutations.txt M1-M3 killed; client-build-gate exit 0.
Kapsam disi: WaiterPwa kendi "Masa degistir" terimini korur; gerekce secenegi "Misafir masa degistirdi" bir gerekce cumlesi (sunucuya gider, testle kilitli) ve yetki listesindeki "Masa tasima" ad bicimi degistirilmedi. "Masa tasi tamamlandi." emir kipi etiketinden turetilen mevcut bildirim kalibi (tum eylemler ayni); eylem basina ayri basari cumleleri istenirse ayri is.
Elle senaryo: kasiyer girisinde baslik "Giris yap"; dolu masada Masa tasi > pencere basligi "Masa tasi" > Onayla > "Masa tasi tamamlandi."

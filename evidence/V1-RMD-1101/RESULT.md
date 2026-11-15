# V1-RMD-1101 sonuc
Dogrulama (kod): renderPaid yalniz "Fisi yazdir" ve "Bekleyen hesaplara don" sunuyordu; masa/hizli satis bilgisi (state.billTable, state.billRead) zaten vardi. Alarm gercekti.
Duzeltme: split-payment.js nextJobLink; masa hesabi birincil "Masalara don" (/tables), hizli satis birincil "Yeni satis" (/); fis ve bekleyen hesaplar ikincil. Fis okunamadiysa (yer bilinmiyor) ve fis onayi acikken (Evet, yazdir birincil kalir) bu dugme gosterilmez.
Kanit: red.log 3 kirmizi; green.log split-payment 30 dosya 340/340; mutations.txt R1-R5 killed; client-build-gate: Cashier vanilla, uretim derlemesi gerektiren istemci degisikligi yok.
Acik risk: fis okuması gelince dugmeler bir kez asagi kayar (hesap okunana kadar fis birincildir); yanlis dokunus yalniz sonraki ise gider, hesap zaten odendi. Tam paralel kosuda split-payment-discount-pending dosyasi zaman asimi ile kirilgan (tek basina gecer, bu gorevle ilgisiz).

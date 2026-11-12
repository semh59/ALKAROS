# V1-RMD-1017 - Oturum suresi dolunca hatirlanan masa ve hesap silinir

Duzeltme: Cashier.tsx forgetShiftState (current-bill-id + selected-table) hem Cikis'ta hem 401 ile oturum bitince (daha once giris yapilmissa) calisir; logout'taki iki satir bu islevle degisti.
Kanit: genisletilmis oturum-bitti testi once kirmizi; Cashier* 19 dosya 103/103; client-build-gate exit 0; gercek tarayici: cerezler silinip urune basilinca "Oturumunuz sonlandirildi", iki anahtar null (oturum-bitti.png). Mutasyon 1/1 killed.

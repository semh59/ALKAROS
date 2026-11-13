# V1-RMD-1028 - Bolme ekrani basligi fatura kodu yerine masayi gosterir

Duzeltme: BillSplitWorkspace.tsx h2 = "Masa X" (masa yoksa "Hesap"); fatura kodu durum satirinda "Hesap no ..." olarak kalir (uzun kod dar ekranda kirilir); olu .bill-split__table kurali silindi.
Kanit: guncellenen test once kirmizi; billing 90/90; client-build-gate exit 0. Mutasyon 3/3 killed.
Gercek ekran: 8090 eski derleme; kod ve test kaniti (kullanici izniyle).
Acik (urun karari): masasiz hesapta baslik yalniz "Hesap"; ust etiket "HESAP / DAGITIM TASARIMI" kasiyer icin jargon.

# V1-RMD-1100 sonuc
Dogrulama (kod): Cashier.tsx ust cubugu "Kasiyer · Diger" sabit metindi; kabuk rolu workspace.tsx'te capability'den turetiliyordu (catalog.manage Yonetici, bills.split Kasiyer / Operasyon, aksi Sinirli erisim). Alarm gercekti.
Duzeltme: strings.ts roleLabelFor(capabilities) tek turetme; workspace.tsx ve Cashier.tsx ayni islevi kullanir. Ust cubuk "<rol> · Diger ▾" bicimini korur; kasiyer icin etiket "Kasiyer / Operasyon" olur (kabukla ayni).
Kanit: red.log (3 kirmizi), green.log src/routes+shell 36 dosya 400/400, mutations.txt R1-R4 killed, client-build-gate.log exit 0 (lint, tip denetimi, vite build).
Acik risk: .operator-card'da nowrap/kirpma yok; uzun "Kasiyer / Operasyon" etiketi dar ekranda iki satira cikabilir (kirpma yok, icerik kaybolmaz), gercek tarayicida 768/1024 px olculmedi.

# V1-RMD-1025 - Hesap bolmede Kisi ekle sayiyla birden cok kisi ekler

Duzeltme: BillSplitWorkspace.tsx "Eklenecek kisi sayisi" alani (1-20, varsayilan 1) + Kisi ekle secilen sayida kisiyi tek dokunusla ekler (addPeople); alan her eklemeden sonra 1'e doner; stil billing.css.
Kanit: 2 yeni test once kirmizi; billing 89/89; tsc/lint/vite build (client-build-gate) exit 0; gercek tarayici /billing: sayi 4 + Kisi ekle -> 1.-4. Kisi eklendi (kisi-sayisi-4-kisi-eklendi.png). Mutasyon 2/2 killed.
Qa sonrasi: alan etiketi 'Eklenecek kisi sayisi'; odakta secilir, Enter ekler, yeni kisiler esit bolmede secili (test + mutant 5/5); billing 16/16 dosya testi, gate exit 0.

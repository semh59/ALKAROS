# V1-RMD-1130 sonuc
Dogrulama: gercekti; urun dokunusunda tum izgara kilitleniyor, ardisik dokunuslar kayboluyordu.
Duzeltme: Cashier.tsx - secenek grubu olmayan urunlerin dokunuslari sirali kuyruga alinir, her biri onceki yanitin revizyonuyla bir kez gonderilir; hata/5xx de kuyruk dusurulur ve yeniden gonderilmez, kasiyere "N dokunus eklenmedi" denir; 409 da kuyruk duser; siradaki dokunus sayisi gorunur. Secenekli urun ve mutfaksiz satis kilitli kalir.
Kanit: red.log 3 kirmizi; green.log 24 dosya/157 test yesil; M1-M9 killed (cp+cmp); client-build-gate exit 0.
Acik risk (dusuk): mutfaksiz kilit testi yok; okuma penceresindeki dokunus eski revizyonla gidebilir (409 gorunur uyari); sunucu mesaji o durumda gorunmez.

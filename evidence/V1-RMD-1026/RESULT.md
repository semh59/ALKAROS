# V1-RMD-1026 - Gonderilmemis sepet varken de mutfaksiz ekleme secilebilir

Duzeltme: Cashier.tsx kitchenlessAvailable = siparis var ve (gonderilmis veya en az bir gonderilmis kalemi var); dugme, neden secici ve masa panelinden gelen niyet artik gonderilmemis sepetle de calisir. Hic gonderilmemis taslakta gizli. Sepet mutfaga gonderilince mod sifirlanir.
Kanit: 3 yeni test + 1 guclendirilen test once kirmizi/yesil; Cashier* 19 dosya 107/107; client-build-gate exit 0 (lint, tsc, vite build). Mutasyon 4/4 killed.
Gercek ekran: 8090 eski derleme; kod ve test kaniti (kullanici izniyle).
Acik (urun karari): mod aciksken "Mutfaga gonder" dugmesi birincil kaliyor; secenekli urun modda eklenemiyor.

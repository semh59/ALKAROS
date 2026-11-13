# V1-RMD-1030 - Plani olmayan bolge icin kat plani istegi sayfa basina bir kez sorulur

Duzeltme: workspace.tsx zonesWithoutPlan bilesen ref'inden modul duzeyi kumeye tasindi; masa ekrani her acilisinda ayni 404'u tekrar istemez. Yenile ve bolge secimi kumeyi temizleyip yeniden sorar; plan kaydedilince bolge kumeden cikar.
Kanit: yeni test once kirmizi (eski kodla); workspace+tables 132/132 -> 133/133 sonrasi 55/55 workspace; client-build-gate exit 0. Mutasyon 4 killed, 1 survived (kayit sonrasi delete, eski davranis, testi UI ile kurulmasi agir).
Kapsam disi: cash-sessions/active 404 "kasa kapali" anlaminda sunucu sozlesmesi (40'tan fazla test ve 3 istemci bagli); degistirmek urun/sozlesme karari.
Gercek ekran: 8090 eski derleme; kod ve test kaniti (kullanici izniyle).

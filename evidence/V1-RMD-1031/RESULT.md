# V1-RMD-1031 - Bekleyen hesaplar rozeti acik liste ile ayni sayiyi gosterir

Duzeltme: PendingChecksWorkspace her okumada onCount ile liste sayisini bildirir; workspace.tsx rozet, kuyruk ekraninda liste sayisini, diger ekranlarda 20 sn yoklamayi kullanir; ekrandan cikinca liste sayisi sifirlanir.
Kanit: iki yeni test once kirmizi; workspace+pending-checks 107/107; client-build-gate exit 0; mutasyon 4/4 killed.
Gercek ekran: 8090 eski derleme; kod ve test kaniti (kullanici izniyle).
Acik: liste okunamazsa rozet son sayida kalir; iki ayri yoklama kopyasi (Cashier.tsx ve menu) birlestirilmedi; liste okuma yarisi (unmount sonrasi onCount) eski davranis.

# V1-RMD-1122 sonuc
Dogrulama: rapor dogru; sayfa yenilenince tenderIdempotencyKey sifirlaniyordu (yalniz bellekte).
Duzeltme: anahtar sessionStorage'da {baglam, anahtar} olarak saklanir; baglam = hesap|yontem|tutar|alinan para|musteri|not|kalan tutar. Ayni baglam -> ayni anahtar (agi hatasi ve yenileme); gercek sunucu yaniti (basari, kesin ret, Declined) -> temizlenir; baglam farkliysa yeni anahtar. Kalan tutar baglamda: istek sunucuya ulastiysa kalan azalir, anahtar tekrar kullanilmaz. Depolama kullanilamazsa bellek anahtari calisir.
Kanit: red.log 1 kirmizi (yenileme sonrasi ayni anahtar); green.log 8 yesil; green-full.log tum tests/Clients/StaticApps; mutations.txt M1-M6 killed (cp+cmp).
Acik risk: iki sekme ayni sessionStorage'i paylasmaz (sekme basina), ayni sekmede yenileme kapsanir; kayit 10 dk sonra gecersiz (eski odeme anahtarinin yeni odemeye uygulanmasini onler); sunucu-tarafi idempotency tekrar davranisi gercek DB ile calistirilmadi.

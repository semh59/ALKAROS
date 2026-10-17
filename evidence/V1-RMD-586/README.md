# V1-RMD-586 kanıtı

Mutfak yönetimi ve Sistem sağlığı ekranları, sağlık ölçümü 3 dakikadan eskiyse (veya zamanı okunamıyorsa) "Sağlık ölçümü eski" uyarısını ve son ölçüm zamanını gösterir. Taze ölçümde uyarı yoktur.

- Testler: tüm PosTerminal paketi 76 dosya / 649 test geçti (`test-runs.log`); `tsc --noEmit` ve `npm run lint` exit 0.
- Mutasyon: 9 mutant (eşik sınırı, okunamayan tarih, eşik değeri, işaret, iki ekranda uyarının kapatılması ve tersine çevrilmesi), 9'u öldü, sağ kalan yok (`mutations.log`, betik `mutate.py`).
- Gerçek tarayıcıda veya Docker'da ekran görüntüsü alınmadı: doğrulanamadı; doğrulama jsdom bileşen testleridir.
- Veritabanı tamamen çökerse ekranlar zaten açılmaz; o durumu V1-RMD-585 bekçisi bildirir.

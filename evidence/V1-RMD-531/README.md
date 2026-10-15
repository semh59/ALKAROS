# V1-RMD-531 kanıtı

- Taze sayfa açılışında ilk oturum yoklaması 401 dönse bile "Oturumunuz sonlandırıldı" mesajı gösterilmez; yalnız daha önce oturum açılmışsa gösterilir (PosTerminal Cashier).
- /cashier/ sayfasında oturum yokken mesajın yanında "Giriş sayfasına git" bağlantısı çıkar (href "/").
- Test: PosTerminal Cashier* 14/14, StaticApps 86/86 (`test-runs.log` son PosTerminal koşusunu içerir); `tsc` temiz; mutasyon 4/4 öldü.
- Kaynak: Docker yığınının gerçek ekran görüntüsü (headless Chrome, http://localhost:8090) — giriş formu yanlış hata gösteriyordu.
- Doğrulanmadı: giriş yapıldıktan sonraki ekranlar görsel olarak denenmedi (kimlik bilgisi girilmedi).

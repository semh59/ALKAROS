# V1-RMD-451 - kabul kanıtı

- PosTerminal `pnpm run typecheck`, `pnpm run lint` ve `pnpm test` exit code 0 (`typecheck.log`, `lint.log`, `vitest.log`).
- İzinli ve izinsiz oturum: kabuk "Ayar geçmişi" sekmesini yalnız `settings.manage` olan oturuma listeler. Kayıt yetkisi
  değiştirilince test kırmızı (`mutation-permission-gate.log`); dosya geri yüklendi ve `cmp` ile aynı doğrulandı.
- Bölüm salt okunurdur: testler ekranda "Geçmişi göster" dışında hiçbir düğme olmadığını doğrular; ayar değiştirme bu görevin dışında.
- Testler ayrıca: ayar adlarının ve evet/hayır değerlerinin Türkçe gösterilmesi (ham anahtar ve `true` çıkmaz), sunucunun varsayılan
  gönderdiği 11 ayarın hepsinin Türkçe adı olması, bilinmeyen anahtar için nötr ad, geçmişin en yeniden eskiye sıralanması,
  değiştiren, önceki/yeni değer ve gerekçenin görünmesi, boş geçmiş, sunucunun Türkçe gerekçesinin aynen görünmesi.
- Gerçek deneme (`gercek-deneme.log`, `gercek-deneme-ayar-gecmisi.png`): temiz PostgreSQL veritabanı, gerçek Host, tarayıcıdan gerçek
  giriş. Sunucunun varsayılan 11 ayarı listelendi; bir ayar iki kez değiştirilip geri alındı ve geçmişte üç satır göründü (ilk kayıt,
  değişiklik, geri alma). Deneme iki hata yakaladı ve düzeltildi: 9 garson ayarının Türkçe adı yoktu ("Tanımsız ayar" görünüyordu),
  sunucunun ilk kayıt gerekçesi İngilizce ("Initial registration") ham görünüyordu; artık "İlk kayıt".
- Bilinen sınır: geçmişte değiştiren kişi sunucuda yalnız kullanıcı kimliği olarak tutuluyor ve kimlik-ad çözümleyen bir uç yok;
  ekran kimliğin ilk 8 karakterini "Kullanıcı xxxxxxxx" olarak gösterir. Yeni bir ayar eklenirse Türkçe adı bu bölümün sözlüğüne eklenmelidir.
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla Yönetim > Ayar geçmişi'nde bir ayarın "Geçmişi göster" düğmesine basın.
  `settings.manage` izni olmayan bir oturumda sekmenin görünmediğini görün.

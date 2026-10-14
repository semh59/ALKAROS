# V1-RMD-521 kanıtı

Doğrulananlar (jsdom + enjekte edilmiş fetch; gerçek tarayıcı/Host yok):
- Kasa sayfası: kalan vardiya iade bütçesi gösterilir; bütçe 0, açık oturum yok veya okunamıyorsa gösterilmez ve iade akışı etkilenmez.
- Bütçeden tamamlanan iadede "İade vardiya bütçenizden tamamlandı."; kartlı iadede fiş numarası istenir; bütçe yetmeyen iadede eski "onay bekliyor" mesajı.
- PosTerminal iade paneli aynı mesajları verir; kalan bütçeyi gösterir.
- Müdür (onay yetkisi olan), bekleyen isteğin yanında isteği açan kasiyere vardiya bütçesi verir; geçersiz/negatif tutar düğmeyi kapatır; virgüllü tutar kabul edilir; sunucu reddi sabit Türkçe cümleyle gösterilir (kendine verilemez, düşük tutar, açık oturum yok).
- Sonuç: PosTerminal tsc temiz, 66 dosya / 535 test, vite build 0; StaticApps 9 dosya / 74 test.

Doğrulanmayanlar: gerçek tarayıcıda görünüm. Sınır: müdür bütçeyi yalnız bekleyen bir iade isteğinden verebilir (ayrı kasiyer seçici yok).

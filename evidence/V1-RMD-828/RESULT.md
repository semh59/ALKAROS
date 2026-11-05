# V1-RMD-828 sonuç

Kapatılan: (1) `WaiterPwa/wwwroot/index.html` Google Fonts'a üç dış istek atıyordu; satırlar kalktı (yazı tipi yığını `waiter-app.css` içinde sistem yazı tipine düşer), `sw.js` önbellek sürümü v21 → v22 (kurulu uygulamalar eski index.html'i atar; activate eski önbellekleri siler, bu incelemede doğrulandı). Test uygulamanın kendi sayfa, stil ve betiklerinde dış adres olmadığını kilitler (SVG xmlns hariç). (2) F0432: ürün aramasında "I" tr-TR'de "ı" olduğu için "isparta" "Isparta"yı bulmuyordu; iki taraf tr-TR küçültülüp "ı" "i"ye katlanıyor ("ıspar", "İSTANBUL", "iğdır" bulunur, "corba" "Çorba"yı bulmaz çünkü ç ayrı harf).

## Doğrulama
- StaticApps 32 dosya, 357/357 yeşil; yeni dosya `waiter-pwa-search-and-fonts.test.js` 11 test (kırmızı: dış adres, 3 arama, önbellek sürümü kırıldı).
- Mutasyon 6 mutant (katlama, sorgu katlaması, ad katlaması, font satırının dönmesi, yazı tipi yığını, önbellek sürümü) hepsi öldürüldü.
- Çalışma ağacında `PosTerminal/dist-nfc/index.html` (gitignore'lu eski derleme çıktısı) hâlâ Google Fonts içeriyor; kaynak dosyalarda yok (`git grep` 0), yeniden derlemede kalkar.

## İncelemeler (qa-frontend, qa-guvenlik-yetki)
Giderilen: önbellekteki eski index.html riski (sürüm artışı), css yorumu. Kabul edilen: `Inter` yığının başında kalır (cihazda kuruluysa kullanılır; indirilmez); aksanlı harfler (ş ğ ç ö ü) aramada ayrı harf sayılır (İngilizce klavyeyle "sogus" "soğuş"u bulmaz); aynı "I" katlaması kasa ve React ekranlarında V1-RMD-835'te.

# V1-RMD-565 - Yönetim ekranı: hata listesi

"Hatalar" sekmesi (reports.view ile listelenir; Gördüm/Çözüldü düğmeleri observability.manage ister) V1-RMD-539 uç noktalarını kullanır: süzgeç (katman/önem/durum), sayfalama, ayrıntı (son oluşumlar), onayla/çözüldü, Telegram kanalı durumu.

`test-runs.log`: ekran testleri 14/14, tüm PosTerminal 561/561, tsc exit 0. `mutations.log`: 5 mutant, hepsi öldü (yetki koruması, süzgeçte sayfa sıfırlama, ham hata metni, ham enum, onay durumu).
İngilizce sızıntı taraması (UI_STYLE_GUIDE §4): tek eşleşme `models.ts` içindeki `"browser.unknown"` sözlük anahtarıdır, ekranda görünmez; görünen metinlerde sızıntı 0.
Sözlükte olmayan hata türü "Bilinmiyor" görünür.

Doğrulanamadı: gerçek sunucuya bağlı tarayıcı denemesi yapılmadı; istemci yalnızca sahte istemciyle test edildi. Onayla/çözüldü isteğinde idempotency anahtarı gönderilmez (sunucu durum geçişi zaten tekrarlanabilir; ikinci çağrı 404 döner).

# V1-RMD-683 — Ayar değer doğrulaması

Sonuç: V1-RMD-598 bulguları R4, R5, R6, R13, R15 kapandı (yeniden etkinleştirme ucu ve bozuk değer günlüğü hariç, aşağıya bak). 16/16 mutant öldü.

Ne değişti:
- **Aralık** (sunucu reddeder, Türkçe mesajla 400 `VALUE_OUT_OF_RANGE`): masa sınırı 0-50; mutfak yoğun eşiği 1-99; QR onay süresi 1 dakika-24 saat (0 veya negatif artık yazılamaz, bekleyen QR siparişlerini toplu reddetme yolu kapandı); işletme adı en çok 80 karakter ve denetim karakteri yok; vurgu rengi yalnız hazır renklerden (PUT ucunda; eski paletten kalmış değer okurken hâlâ varsayılana düşer).
- **Süre biçimi**: yalnız `gg.ss:dd:ss` veya ISO ("PT30M"); "5" artık 5 gün sayılmaz; sıfır ve negatif süre hiçbir ayarda kabul edilmez.
- **Uzunluk**: metin değeri en çok 2000, JSON 16000, gerekçe 500 karakter.
- **Pasif ayar**: değiştirme ve ikinci kez pasifleştirme 409 `SETTING_INACTIVE`; geçmişe ek satır yazılmaz.
- **Kayıt yarışı**: `INSERT … ON CONFLICT DO NOTHING`; 8 paralel kayıttan tam biri kazanır, diğerleri `DuplicateSettingKeyException` (ham veritabanı hatası yok).
- **Sır yasağı**: ayırıcılardan bağımsız (`api-key`, `x.api.key`, `pass_wd`, `pwd`, `passphrase`, `client-secret`).
- **İzin sabiti**: `ApplicationPermissions.SettingsManage`; ayar ve müşteri kredi uçları onu kullanır (Codes listesine bilerek eklenmedi; yetki göçle verilir).

Kapsam dışı kalan (yeni görev): ayarı yeniden etkinleştirme ucu (arayüz ISettingsRepository değişikliği ister, bu görevin yüzeyinde yoktu), bozuk kayıtlı değerde uyarı günlüğü. Bugün hiçbir ekran ayar pasifleştirmiyor; yalnız DELETE API'si var.

Kanıt: `test-typedsettings.log` (44/44), `test-host-settings.log` (25/25), `test-BusinessIdentity.log` (24/24), `test-errorcatalog.log` (180/180), `test-pending.log` (12/12), diğer ayar modül projeleri yeşil; `mutations.log` 16/16 KILLED.
Doğrulanamadı: tarayıcıda elle deneme (ekran V1-RMD-684'te).

# V1-RMD-562 kanıtı

Karar (PO, 2026-10-15): izleme verisinde kişisel ve ödeme bağlamı (ad, telefon, e-posta, IBAN, kimlik numarası, tutar, işlem kimliği) ile hata kodları geçebilir; sistem dışarıya veri çıkarmıyor. Önceki "kişisel/ödeme verisi geçmesin" kuralı bununla yürürlükten kalkar.

## Değişen
- `TelemetryPrivacyGuard`: izin listesi kalktı; gizli adlı alanlar (parola, token, gizli anahtar, api anahtarı, CVV, kart numarası, Authorization, cookie) hiç saklanmaz; kart numarası (Luhn), Bearer ve JWT değerleri `[gizlendi]` olur; e-posta, telefon, IBAN, TC kimlik, tutar geçer. 200 karakter sınırı sürer.

## Aynı kalan sınırlar
- Telegram mesajı yalnız sabit künye taşır (V1-RMD-538); dışarı çıkan tek kanal budur.
- Kart numarası, CVV, parola, token, gizli anahtar hiçbir koşulda saklanmaz.
- Saklama süreleri sürer; KVKK silme talebinin izleme tablolarına uygulanması ayrı iş olarak açıktır.

## Kanıt
- TelemetryPrivacy testleri 17/17, ErrorEvents 8/8, ErrorNotifications 15/15 geçti; mutasyon 5/5 öldürüldü.
- Doğrulanamadı: `TelemetryPrivacyGuard` yalnız hata olayı ayrıntısında kullanılıyor; yeni dinleyiciler eklendiğinde kullanımları o görevlerde sınanır.

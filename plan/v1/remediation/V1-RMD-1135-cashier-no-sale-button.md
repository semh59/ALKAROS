# V1-RMD-1135 - Kasa ekranında "Satış yok" düğmesi: gerekçe listesi, uyarı ve sonuç bildirimi

- Task ID: V1-RMD-1135
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer, kasa oturumu ekranında (Cashier, `cash-session.js`) para işlemi olmadan çekmeceyi "Satış yok" düğmesiyle açabilsin. V1-RMD-1134 uçlarını kullanır.

1. Ekran oturum açılınca durum ucunu (`GET .../cash-drawer`) okur; düğme YALNIZ `Enabled` ve `CanKick` ikisi de doğruysa görünür. Durum okunamazsa düğme görünmez (kasa işlemi etkilenmez). Düğme etiketi "Satış yok" (14 px altına inmez; dokunma hedefi 48 px yükseklik).
2. Düğmeye basınca gerekçe formu açılır: hazır liste "Bozuk para", "Sayım", "Diğer" (düğme grubu veya seçim, bir seçim zorunlu), not alanı ("Diğer" seçilince zorunlu, en çok 200 karakter, "Not" etiketi) ve "Çekmeceyi aç" / "Vazgeç" düğmeleri. Seçim yoksa veya "Diğer"de not boşsa istemci Türkçe cümleyle durdurur (sunucu aynı kuralı uygular).
3. Gönderim `IdempotencyKey` ile yapılır; istek sürerken düğme kilitlidir (çift dokunuşta ikinci istek gitmez). Sonuç bildirimleri sabit Türkçe cümledir: açıldı ("Çekmece açıldı."), açılamadı ("Çekmece açılamadı; yazıcı bağlantısını kontrol edin."), uyarı varsa sunucunun Türkçe uyarısı gösterilir, ayar kapalı, yetki yok, bağlantı yok ve genel hata mevcut hata sözlüğü kalıbıyla (ham istisna veya HTTP kodu görünmez).
4. Başarıda form kapanır ve sayaç ("Bu vardiyada satışsız açma: N") durum satırında güncellenir; hiçbir kasa tutarı değişmez.

## Owned surface

- `evidence/V1-RMD-1135/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - src/Clients/Cashier/wwwroot/cashier-app.css
  - tests/Clients/StaticApps/cash-session-no-sale.test.js
  - tests/Clients/StaticApps/cash-session-text-size.test.js
  - tests/Clients/StaticApps/cashier-touch-targets.test.js
  - tests/Clients/StaticApps/error-dictionary-lock.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci testleri (tests/Clients/StaticApps)

## Out of scope

- Sunucu uçları ve sözleşmesi; ayar ekranı; PosTerminal React kasa ekranı; fiziksel cihaz denemesi

## Dependencies

- V1-RMD-1134

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- V1-RMD-1136

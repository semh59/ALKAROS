# V1-RMD-538 kanıtı

Bildirim kuyruğu (Postgres, migration 188), dağıtıcı ve Telegram kanalı. Yalnız sabit künye gönderilir: kategori, sayı, saat, terminal etiketi.

## Kabul koşulları
1. Kritik değişiklik kuyruğa girer ve dağıtıcı çalışınca tek mesaj olarak gider (CriticalChangeIsQueuedAndSentOnceWithFixedTokensOnly). "5 sn / 60 sn" süreleri gerçek zamanlı ölçülmedi; zamanlama işi V1-RMD-557'de Host'a bağlanınca ölçülür (doğrulanamadı).
2. Bot anahtarı: kanal hiçbir hata yolunda istek adresini taşımaz; taşıma hatasında sonuç yalnız sabit "tekrar dene" olur (TelegramFailureNeverCarriesTheBotKey). Günlük yazımı yok.
3. 100 birikmiş bildirim 1 özet mesajı olur (HundredPendingNotificationsBecomeOneDigestMessage).
4. Ek davranışlar: 3 deneme sonra Failed, 429 bekleme süresi (gövdeden okunur), 30 dk tekrar bastırma, yapılandırılmamış kanal kuyruğu korur, Info kuyruğa girmez.
5. Test 11/11 gerçek Postgres'te geçti (`test-runs.log`); mutasyon 9/9 öldürüldü (`mutations.log`).

## Doğrulanamadı / devredilen
- Gerçek Telegram'a gönderim yapılmadı (V1-RMD-557). Bot anahtarı ve sohbet kimliği geri çağrıyla verilir; gizli bilgi sağlayıcısına bağlama ve "kanal kapalı" ekran uyarısı Host/ekran işinde yapılır.
- Kuyruk ve bastırma tablolarının saklama süresi V1-RMD-556'da eklenir.

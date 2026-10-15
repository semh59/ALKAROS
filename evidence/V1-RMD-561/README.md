# V1-RMD-561 kanıtı

Bildirim teslimatı Host'a bağlandı: hata olayı yazıcısının alıcısı artık birleşik (uyarı köprüsü + bildirim kuyruğu); 30 sn'lik dağıtıcı arka plan işi çalışır; Telegram bilgisi yapılandırmadan (`Monitoring:Telegram:BotKey`, `Monitoring:Telegram:ChatId`; ortam değişkeni `Monitoring__Telegram__BotKey` / `__ChatId`) okunur, ikisinden biri yoksa kanal kapalıdır ve kuyruk satırları bekler. HTTP istemcisi fabrikasız oluşturulur (istek adresi, dolayısıyla bot anahtarı hiçbir günlüğe yazılmaz).

## Kabul koşulları
1. Yazıcıya düşen Kritik olay kuyruğa girer, dağıtıcı tek mesaj yollar; mesajda ham metin yok (CriticalEventReachesTheQueueAndTheDispatcherSendsOneMessage); Host kayıt zinciri (AddServerErrorDetection + AddNotificationDelivery) gerçek yazıcıdan kuyruğa akar (HostRegistrationFeedsTheNotificationQueueFromTheRealWriter).
2. Bir alıcı hata verse diğeri çağrılır (FailingSinkDoesNotStopTheOthers); yapılandırma yoksa kanal kapalı (ChannelIsOffWithoutBothSettingsAndOnWithThem).
3. Test 4/4 geçti; mutasyon 4/4 öldürüldü; DualScreenHostTests 6/6 geçti.

## Doğrulanamadı / sınır
- Gerçek Telegram gönderimi yapılmadı (V1-RMD-557, sizin bot hesabınız gerekir).
- Çöküş kanalı (552) ve ekranda "kanal kapalı" uyarısı (539) hâlâ bağlı değil.
- Uygulamanın tamamı ayağa kaldırılıp uçtan uca bildirim akışı denenmedi; kayıt zinciri test hizmet koleksiyonunda sınandı.

# V1-RMD-554 kanıtı

Güvenlik dedektörleri: 60 sn'de 10 başarısız giriş (olayda kullanıcı adı yok, yalnız terminal etiketi), 60 sn'de 20 yetki reddi, outbox'ta yeni kalıcı başarısız (dead) mesaj.

## Kabul koşulları
1. 60 sn'de 10 başarısız giriş 1 olay üretir; 9 veya yavaş girişler üretmez; etikette yalnız `terminal:<8 hane>` (TenFailedLoginsInAMinuteProduceOneEventWithTerminalLabelOnly, NineFailuresOrSlowFailuresProduceNoEvent).
2. Outbox'ta yeni dead mesaj olay olur, aynı sayı tekrar olay üretmez; yoklama 30 sn (NewDeadLetterProducesOneEventAndUnchangedCountProducesNone). "1 dakika içinde" yoklama aralığından çıkar; gerçek süre ölçülmedi.
3. Yetki reddi: her ret önce mevcut kalıcı kayda gider, sonra sayılır (DenialBurstIsReportedAndEveryDenialStillReachesTheDurableSink).
4. Test 4/4 gerçek Postgres'te geçti; mutasyon 7/7 öldürüldü.

## Doğrulanamadı / devredilen
- Giriş ucundaki bağlama ve DI kaydı derlendi, ancak çalışan uygulamada uçtan uca bir başarısız giriş serisi denenmedi.
- QR tüneli sinyali V1-RMD-558'e taşındı (bağlayıcı ayrı süreç). İş kuralı ihlali dedektörü planda kapsam dışı bırakıldı.
- Ek: V1-RMD-534 ara katmani yazici yokken calisir hale getirildi (DualScreenHostTests bare host kaydi 534 sonrasi kirilmisti; 554 sirasinda yakalandi, DualScreenHostTests 6/6 gecti).

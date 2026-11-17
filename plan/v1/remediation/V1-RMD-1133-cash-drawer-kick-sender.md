# V1-RMD-1133 - Fiziksel kasa çekmecesi: yazıcıya çekmece darbesi (ESC p) gönderen ayrı gönderici

- Task ID: V1-RMD-1133
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Sunucu, kasa yazıcısına TCP (ham yazıcı portu, genelde 9100) üzerinden çekmece açma darbesini gönderebilsin. Kasa yazıcısı mutfak fişi hattından ayrı tutulur: mevcut ESC/POS fiş biçimleyicisine ve `IPrinterTransport` sözleşmesine DOKUNULMAZ (her mutfak fişi çekmeceyi açmasın; sözleşme değişirse tüm sahte tanımlar etkilenir).

1. `EscPosCashDrawer.PulseBytes`: tam olarak `1B 70 00 19 FA` (ESC p, pim 0, açık süresi 50 ms, bekleme 500 ms; Epson uyumlu yazıcılarda standart komut).
2. `ICashDrawerKicker.KickAsync(host, port, cancellationToken)` -> `CashDrawerKickResult(Delivered, Failure)`. Hata istisna olarak sızmaz: bağlantı reddi, ağ hatası ve zaman aşımı `Delivered = false` ile kısa İngilizce `Failure` metni döner (çağıran Host kaydeder ve denetler; sessiz yutma yok). Çağıranın iptali de `Delivered = false` döner. Tüm gönderim (bağlan + yaz + boşalt) 2 saniyeyle sınırlıdır; yazıcı durum sorgusu (DLE EOT) YAPILMAZ ve otomatik yeniden deneme YAPILMAZ (çekmece darbesinin tekrarı zararsızdır ama ödeme yolunu bekletmemek için tek deneme).
3. `TcpCashDrawerKicker` gerçek gönderici; `KitchenModule` içinde kayıtlıdır. Tüm yazıcı profilleri (Epson, Bixolon, Star, Ascii) için aynı baytlar kullanılır: Star ve Xprinter ESC p davranışı cihazsız doğrulanamadı; özellik varsayılan KAPALI olduğundan risk gerçek cihaz denemesine (V1-RMD-1136 kılavuzu) bırakılır ve raporlanır.
4. Testler gerçek cihaz gerektirmez: loopback `TcpListener` alınan baytları birebir karşılaştırır; reddedilen port, iptal edilmiş belirteç ve hiç okumayan dinleyici ile zaman aşımı yolu sınanır.

## Owned surface

- `evidence/V1-RMD-1133/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Modules/Kitchen/PrintQueue/CashDrawerKickTests.cs (V1-KIT-003) — yeni dosya: bayt ve loopback testleri.
  - src/Modules/Kitchen/PrintQueue/CashDrawerKick.cs (V1-KIT-003) — yeni dosya: çekmece darbesi ve gönderici.
  - src/Modules/Kitchen/KitchenModule.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. Kitchen.PrintQueue testleri (bayt, loopback, hata yolları)

## Out of scope

- Ayar okuma, yetki, denetim kaydı, uç noktalar (V1-RMD-1134); mevcut fiş biçimleyici ve `IPrinterTransport`; profil başına pim/süre ayarı; kuyruk tablosu

## Dependencies

- V1-RMD-1131

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- V1-RMD-1134

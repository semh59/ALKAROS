# V1-RMD-547 kanıtı

Konteyner içinden görünen kaynaklar izlenir: disk (%90 Warning, %95 Critical), bellek (%90 Warning), anormal SignalR kopmaları sayılır. 60 sn aralıklı iş `DualScreenApplication.cs` içinde kayıtlı; kopma sayacı tüm hub'lara genel süzgeçle takılır (süzgeç yalnız gözler, kopma aynen sürer).

## Kabul koşulları
1. Disk %90 eşiği olay üretir; altı üretmez (DiskAtNinetyPercentRaisesWarningAndBelowRaisesNothing, DiskAtNinetyFivePercentIsCritical).
2. Bağlantı kopma sayacı hatayla biten kopmada artar, temiz kapanışta artmaz (OnlyErrorDisconnectsAreCountedAndTheDisconnectAlwaysContinues). Gerçek bir SignalR bağlantısı kopartılarak ölçülmedi (doğrulanamadı); süzgeç hub yaşam döngüsü bağlamıyla birim düzeyinde sınandı.
3. Test 4/4 gerçek Postgres'te geçti; mutasyon 6/6 öldürüldü.

## Sınırlar / devredilen
- Ana makine (Docker Desktop/WSL2) kaynakları konteynerden görünmez; yüksek ana makine yükü bu izleyiciyle fark edilmez. Ekranda belirtme V1-RMD-539 işidir.
- Kopma sayacı henüz ekrana veya olaya bağlı değil (yalnız sayıyor); garson çevrimdışı kuyruk uzunluğu V1-RMD-558'e taşındı.
- İşlemci kullanımı okunmuyor (yalnız disk ve bellek).

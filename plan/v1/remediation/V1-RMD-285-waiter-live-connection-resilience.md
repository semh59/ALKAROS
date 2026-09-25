# V1-RMD-285 - Garson canlı bağlantısı kopunca geri döner ve kaçırılanı telafi eder

- Task ID: V1-RMD-285
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`waiter-app.js` içindeki `WaiterOrderStatusHub` bağlantısı yalnız `withAutomaticReconnect([0,1000,3000,5000,10000])` kullanıyor: 5 denemeden (~19 sn) sonra bağlantı kalıcı kapanıyor, sayfa açılışında sunucu ulaşılamazsa `start()` hatası sessizce yutuluyor ve otomatik yeniden bağlanma başlangıç hatasında hiç devreye girmiyor. `onreconnected` işleyicisi yok: bağlantı yokken gelen 'ürün hazır' ve 'misafir sipariş verdi' olayları kaybolur; ekranda bağlantı durumu göstergesi yok. Bu görev bağlantıyı sürekli yeniden deneyen, geri geldiğinde bekleyen QR siparişlerini yeniden yükleyen ve bağlantı durumunu Türkçe gösteren hâle getirir.

## Owned surface

- `plan/v1/remediation/V1-RMD-285-waiter-live-connection-resilience.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/waiter-app.js
  (yalnız `connectHub` ve bağlantı durumu göstergesi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/specs/09-live-connection-resilience.spec.js

## In scope

1. İlk `start()` hatasında ve `onclose` sonrasında sınırlı üstel bekleme ile sonsuz yeniden deneme.
2. Yeniden bağlanınca `loadPending` ile bekleyen QR siparişlerinin yeniden yüklenmesi.
3. Kullanıcıya görünen Türkçe bağlantı durumu (bağlı / yeniden bağlanılıyor / bağlantı yok).

## Out of scope

- Sunucu tarafı değişikliği.
- Kaçırılan 'ürün hazır' olaylarını geriye dönük oynatmak (web push zaten yedek kanaldır).

## Dependencies

- V1-RMD-149
- V1-RMD-201

## Acceptance evidence

- WaiterPwa E2E (gerçek Host + Chromium): sunucu durdurulup 30 sn sonra başlatılınca bağlantı döner ve bağlantı yokken oluşturulan QR sipariş banner'da görünür; sunucu kapalıyken sayfa açılıp sonra sunucu başlatılınca bağlantı kurulur.
- **Mutasyon kontrolü:** yeniden deneme devre dışı bırakılınca test kırılır.
- `plan_audit_tool.py validate` ve `consistency_audit.py` temiz.

## Handoff

- None

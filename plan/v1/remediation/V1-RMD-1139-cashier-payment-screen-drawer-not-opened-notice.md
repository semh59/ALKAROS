# V1-RMD-1139 - Ödeme ekranında "Çekmece açılamadı, elle açın" sakin uyarısı

- Task ID: V1-RMD-1139
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu 2): çekmece açılmazsa ödeme ekranında kasiyere hiçbir uyarı çıkmıyordu; hata yalnız sunucu günlüğünde ve denetim kaydındaydı. V1-RMD-1137'nin `drawerOpened` alanı kullanılır.

1. Nakit ödeme yanıtında `drawerOpened === false` ise Hesap Ödeme sayfası sakin, hata olmayan bir bilgi satırı gösterir: "Çekmece açılamadı, elle açın." (kırmızı hata kutusu ve "Hata" başlığı yok; ödeme başarılı görünümü değişmez; kasiyeri korkutmaz). Satır hesap ödendi ekranında da görünür ve yeni bir tahsilat ya da hesap değişimiyle kalkar.
2. `true`, `null` (çekmece kapalı) ve alanın hiç gelmediği yanıtta hiçbir şey gösterilmez. Satır yalnız nakit ödemeden sonra çıkar; kart, havale ve cari ödemelerde çıkmaz.
3. Metin sabit Türkçe cümledir; metin boyutu ve dokunma kuralları (`docs/CASHIER_UI_RULES.md`: 14 px alt sınır) korunur. Satır ekran okuyucuya `role="status"` ile bildirilir (hata değil).

## Owned surface

- `evidence/V1-RMD-1139/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/split-payment-drawer-notice.test.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - tests/Clients/StaticApps/cashier-touch-targets.test.js
  - tests/Clients/StaticApps/split-payment-failures.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci testleri (tests/Clients/StaticApps)

## Out of scope

- Sunucu sözleşmesi (V1-RMD-1137); "Satış yok" ekranı; yeniden deneme düğmesi; ödeme ekranından çekmeceyi elle açma

## Dependencies

- V1-RMD-1137

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- None

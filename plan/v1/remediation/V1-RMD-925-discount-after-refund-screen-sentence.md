# V1-RMD-925 - Ödeme ekranı "iadeden sonra indirim" hatasının sabit cümlesini bilir

- Task ID: V1-RMD-925
- Status: InProgress
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

`tests/Clients/StaticApps/error-dictionary-lock.test.js` kırık: V1-RMD-909 ile sunucu `DISCOUNT_AFTER_REFUND` (409) kodunu eklemiş, ödeme sayfasının hata sözlüğünde (`ERROR_TEXTS`) bu kod için cümle yok; ekran durum kodu yedeğine düşer. Karar: sözlüğe sunucuyla birebir aynı cümle eklenir ("Bu hesapta kalem iadesi yapılmış; iadeden sonra indirim ya da ikram uygulanamaz."). Kırmızı test mevcut kilit testidir.

## Owned surface

- `evidence/V1-RMD-925/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Sözlüğe cümle; kilit testi yeşil
2. Mutasyon ve iki bağımsız qa

## Out of scope

- Diğer kodlar

## Dependencies

- V1-RMD-924

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

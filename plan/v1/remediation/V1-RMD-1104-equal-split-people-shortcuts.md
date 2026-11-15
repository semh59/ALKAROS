# V1-RMD-1104 - Eşit bölmede 2, 3, 4 kişi hızlı tuşları

- Task ID: V1-RMD-1104
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu: eşit bölme kapalı bir bölümde; kasiyer açıp kişi sayısını yazıp Hesapla'ya basmak zorunda. Tutar alanının yanında "2 kişi", "3 kişi", "4 kişi" hızlı tuşları gelir; dokunuş kişi sayısını ayarlar ve payı tutar alanına yazar (Hesapla ile aynı hesaplama). Mevcut açılır bölüm ve Hesapla korunur.

## Owned surface

- `evidence/V1-RMD-1104/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-equal-split-shortcuts.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Ürüne göre bölme; kayıtlı dağılım; sunucu sözleşmesi

## Dependencies

- V1-RMD-1103

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

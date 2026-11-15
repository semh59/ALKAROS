# V1-RMD-1108 - Tek ödemeli hesapta ürün iadesi ödemeyi kendisi seçer

- Task ID: V1-RMD-1108
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu: ürün bazlı iade formunda hesapta tek geçerli ödeme olsa bile ödeme seçimi boş geliyor. Seçilebilir tek ödeme varsa form onu otomatik seçer; birden çok ödemede seçim boş kalır. Stoğa geri dönüş seçimi varsayılansız kalır (bilinçli karar; DEĞİŞMEZ).

## Owned surface

- `evidence/V1-RMD-1108/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-item-refund-single-payment.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Stok varsayılanı; tam ödeme iadesi kartları; sunucu sözleşmesi

## Dependencies

- V1-RMD-1107

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

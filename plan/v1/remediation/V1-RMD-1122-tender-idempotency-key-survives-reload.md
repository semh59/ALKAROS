# V1-RMD-1122 - Tahsilat işlem anahtarı sayfa yenilenince kaybolmaz

- Task ID: V1-RMD-1122
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 15: Tahsilat isteği sunucuya ulaşıp yanıt kaybolursa kasiyer sayfayı yenileyince işlem anahtarı sıfırlanıyor ve aynı tahsilat yeni anahtarla tekrar gönderilebiliyor (çift tahsilat riski). Anahtar; hesap, yöntem, tutar, alınan para, müşteri, not ve kalan tutar bağlamıyla oturum depolamasında saklanır; ağ hatasında ve sayfa yenilenince aynı bağlam aynı anahtarla yeniden denenir, bağlam değişirse (kalan tutar dahil: istek sunucuya ulaşmışsa kalan tutar azalmıştır) yeni anahtar üretilir; sunucudan gerçek yanıt (başarı veya kesin ret) gelince anahtar temizlenir. Sunucu sözleşmesi ve tahsilat kuralları değişmez.

## Owned surface

- `evidence/V1-RMD-1122/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-tender-key-persistence.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci testleri (tests/Clients/StaticApps)

## Out of scope

- Sunucu sözleşmesi ve idempotency kuralları; iade, indirim, ikram, bahşiş anahtarları

## Dependencies

- V1-RMD-1120

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- None

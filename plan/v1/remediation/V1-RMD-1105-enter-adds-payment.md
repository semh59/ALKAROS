# V1-RMD-1105 - Tutar ve alınan para alanlarında Enter ödemeyi ekler

- Task ID: V1-RMD-1105
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu: tutar veya alınan para alanında Enter hiçbir şey yapmıyor; kasiyer fareye uzanıyor. Ödeme tutarı ve alınan para alanlarında Enter, "Ödemeyi Ekle" ile aynı işlevi çağırır; düğme pasifken ve ödeme sürerken Enter yok sayılır, böylece çift gönderim olmaz.

## Owned surface

- `evidence/V1-RMD-1105/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-enter-key.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Diğer alanlardaki Enter davranışı; sunucu sözleşmesi

## Dependencies

- V1-RMD-1104

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

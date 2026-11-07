# V1-RMD-893 - Kasiyer ekranında bekleyen indirim onaylanınca uygulanabilsin (anahtar 202'de silinmez)

- Task ID: V1-RMD-893
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`submitDiscount` her gerçek sunucu cevabında (202 dahil) idempotency anahtarını siler ve taslak alanlarını temizler; yönetici onayladıktan sonra kasiyer yeni anahtarla tekrar gönderdiğinde yeni bir hibe isteği açılır ve yine bekler, onaylanan hibe hiç kullanılmaz. İkram akışı (`submitComp`) bunu zaten doğru yapıyor: bekleyen istekte anahtarı ve taslağı korur, "Onay durumunu kontrol et" düğmesiyle aynı istek aynı anahtarla yeniden gönderilir. İndirim akışı aynı kalıba getirilir: 202'de anahtar ve taslak korunur, düğme "Onay durumunu kontrol et" olur, onay sonrası aynı anahtarla gönderim 200 ile uygulanır; taslak değişirse yeni anahtar üretilir.

## Owned surface

- `evidence/V1-RMD-893/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/split-payment-discount-pending.test.js — yeni test
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js — indirim anahtarı ve düğme metni
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Gerçek vitest testi: 202 sonrası aynı anahtarla yeniden gönderim (iki istekte aynı IdempotencyKey, ikincisi 200 ile uygulanır); taslak değişince yeni anahtar
2. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Sunucu tarafı; ikram akışı; PosTerminal (React) tarafı

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

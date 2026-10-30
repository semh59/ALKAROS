# V1-RMD-756 - İndirim, bahşiş ve kasa hareketi tekrar anahtarı değişen denemede sıfırlanır

- Task ID: V1-RMD-756
- Status: Done
- Assignee: hermes-session-20261028_015834_6948d340
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu F0068 (register.md:78): ağ hatasından sonra kasiyer indirim, bahşiş veya kasa hareketi formunu değiştirip yeniden gönderirse eski idempotency anahtarı yeni gövdeyle gider; sunucu ilk kaydı döndürür ve yeni tutar işlenmiş sanılır. Aynı gövdeyle tekrar aynı anahtarı, değişmiş gövde yeni anahtarı kullanmalıdır.

## Owned surface

- `plan/v1/remediation/V1-RMD-756-indirim-bahsis-kasa-hareketi-anahtar-sifirlama.md`
- `evidence/V1-RMD-756/**`
- Sınırlı ek: src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek: src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
- Sınırlı ek: tests/E2E/Cashier/specs/18-discount-and-tip.spec.js
- Sınırlı ek: tests/E2E/Cashier/specs/05-cash-session-lifecycle.spec.js

## In scope

- `submitDiscount`, `submitTip` ve `submitCashMovement`: anahtar gönderilen gövdeye bağlanır; gövde aynıysa ağ hatası sonrası tekrar aynı anahtarı, gövde değiştiyse yeni anahtarı kullanır. Gerçek sunucu yanıtında anahtar mevcut davranışla temizlenir.
- Gerçek Chromium + gerçek Host + gerçek PostgreSQL E2E testi: üç formun her birinde aynı gövdeli tekrar aynı anahtarı, değişmiş gövde farklı anahtarı gönderir.

## Out of scope

- Tahsilat (`submitTender`) anahtarı; V1-RMD-314 ile açık sıfırlama noktaları var.
- Sunucu tarafı idempotency davranışı.
- PosTerminal (React) akışları.

## Dependencies

- V1-RMD-314
- V1-RMD-241
- V1-RMD-292

## Acceptance evidence

1. Düzeltmeden önce yeni E2E testleri değişmiş gövdede aynı anahtarı yakalayarak kırmızı olur; çıktı kaydedilir.
2. Düzeltmeden sonra 05, 09 ve 18 spec'leri gerçek ortamda exit 0 geçer.
3. Mutasyon: her formda gövde karşılaştırması kaldırıldığında ilgili test kırmızı olur; dosya hash'i geri yüklenir.
4. Kapsam aracı InProgress commit'ine karşı exit 0 verir; iki bağımsız incelemede artık bulgu kalmaz.
5. Semih senaryosu: Hesap Ödeme ekranında ağ kesikken %10 indirim dene, ağ gelince %20 yapıp uygula; %20 kaydedilir. Kasa Oturumu'nda 50 TL giriş ağ hatası alırsa 70 TL'ye düzeltilip kaydedildiğinde 70 TL işlenir.

## Handoff

- None

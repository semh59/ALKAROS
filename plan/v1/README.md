# V1 - Core Restaurant Operation

## Hedef

Gerçek para kabul etmeyen fakat masa, sipariş, mutfak ve adisyon temelini uçtan
uca çalıştıran çekirdek operasyon.

## Giriş koşulu

`GATE-V1-ENTRY` kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 47 görev dosyasının tamamı `Done` olur; `V1-FND-010` shared fixture provenance blocker'ı
  çözülmeden `GATE-V1-EXIT` kapanmaz.
- `V1-FND-001`, `V1-FND-010`, `V1-FND-003`, `V1-FND-004`, `V1-FND-005`, `V1-SEC-001`,
  `V1-SEC-002`, `V1-FND-002` ve `V1-FND-006` sıralı foundation kapısı geçmeden
  başka application görevi başlamaz.
- Kimlik, yetki, masa, sipariş, mutfak ve bill foundation testleri geçer.
- Duplicate submit ve concurrency senaryoları kanıtlanır.
- Payment UI ve gerçek fiscal akış kapalıdır.
- Audit, print queue ve yerel backup temel akışları geri kazanılabilir durumdadır.

## Modüller

`alerts`, `billing`, `cash-design`, `cashier-ui`, `catalog`, `foundation`,
`identity-authorization`, `kitchen-printing`, `operations`, `orders`,
`reconciliation`, `reporting`, `security-foundation`, `settings`,
`table-management`, `waiter-pwa`, `remediation`.

Doğrulanan plan hacmi: 16 modül, 47 tek-sahip görev.

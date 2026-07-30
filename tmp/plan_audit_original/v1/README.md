# V1 - Core Restaurant Operation

## Hedef

Gerçek para kabul etmeyen fakat masa, sipariş, mutfak ve adisyon temelini uçtan
uca çalıştıran çekirdek operasyon.

## Giriş koşulu

V0 çıkış kapısı kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 36 görev dosyasının tamamı `Done`.
- Kimlik, yetki, masa, sipariş, mutfak ve bill foundation testleri geçer.
- Duplicate submit ve concurrency senaryoları kanıtlanır.
- Payment UI ve gerçek fiscal akış kapalıdır.
- Audit, print queue ve yerel backup temel akışları geri kazanılabilir durumdadır.

## Modüller

`alerts`, `billing`, `cash-design`, `cashier-ui`, `catalog`, `foundation`,
`identity-authorization`, `kitchen-printing`, `operations`, `orders`,
`reconciliation`, `reporting`, `settings`, `table-management`, `waiter-pwa`.

Doğrulanan plan hacmi: 15 modül, 36 tek-sahip görev.

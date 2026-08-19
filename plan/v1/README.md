# V1 - Core Restaurant Operation

## Hedef

Gerçek para kabul etmeyen fakat masa, sipariş, mutfak ve adisyon temelini uçtan
uca çalıştıran çekirdek operasyon.

## Giriş koşulu

`GATE-V1-ENTRY` kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 86 görev dosyasının 81'i `Done`, 1'i `InProgress`, 4'ü onaylı `NotApplicable` durumundadır. Gate kapanışı için InProgress görev ve devraldığı bulgular kapanmalıdır.
- `V1-FND-001`, `V1-FND-010`, `V1-FND-003`, `V1-FND-004`, `V1-FND-005`, `V1-SEC-001`,
  `V1-SEC-002`, `V1-FND-002` ve `V1-FND-006` sıralı foundation kapısı geçmeden
  başka application görevi başlamaz.
- Kimlik, yetki, masa, sipariş, mutfak ve bill foundation testleri geçer.
- Duplicate submit ve concurrency senaryoları kanıtlanır.
- Payment UI ve gerçek fiscal akış kapalıdır.
- Audit, print queue ve yerel backup temel akışları geri kazanılabilir durumdadır.

## Modüller

`alerts`, `billing`, `cash-design`, `cashier-ui`, `catalog`, `foundation`, `governance`,
`identity-authorization`, `kitchen-printing`, `operations`, `orders`,
`reconciliation`, `remediation`, `reporting`, `security-foundation`, `settings`,
`table-management`, `waiter-pwa`.

Doğrulanan plan hacmi: 18 modül/dizin, 86 tek-sahip görev (81 Done, 1 InProgress, 4 NotApplicable).

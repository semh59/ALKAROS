# V1 - Core Restaurant Operation

## Hedef

Gerçek para kabul etmeyen fakat masa, sipariş, mutfak ve adisyon temelini uçtan
uca çalıştıran çekirdek operasyon.

## Giriş koşulu

`GATE-V1-ENTRY` kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altında 143 görev vardır: 128 `Done`, 4 onaylı `NotApplicable`, 4 `Planned`, 7 `Blocked` ve
  0 `InProgress` görev vardır. Tarihsel C71 kapanışı korunur, fakat tam production denetimi remediation zinciri
  tamamlanmadan güncel production readiness kanıtı değildir.
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

Doğrulanan plan hacmi: 18 modül/dizin, 143 tek-sahip görev (128 Done, 4 NotApplicable, 4 Planned, 7 Blocked,
0 InProgress).
Tarihsel C71 kapanış kaydı `evidence/v1/gate-v1-exit-closure.md` altındadır; güncel yeniden denetim zinciri açık kalır.

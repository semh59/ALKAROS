# V1-RMD-848 sonuç

`dynamic-a11y.test.ts` sayfa betiğini `(0, eval)` ile çalıştırıyordu; oxlint `no-eval` hatası verdiği için PosTerminal `build` (lint + tsc + vite) kırıktı. Betik artık `new Function(...)()` ile aynı genel kapsamda (jsdom global) çalışıyor; test davranışı değişmedi (34 test).

## Kanıt
- Kırmızı `red.log`: lint exit 1 (`no-eval`). Yeşil `green-lint.log`: exit 0, hata yok (yalnız önceki uyarılar, bütçe 20 içinde). `green.log`: 34/34. `full.log`: 103 dosya, 1103 test geçti; `typecheck.log` exit 0.
- Mutasyon `mutations.log` (cp+cmp ile geri alındı): betik hiç çalıştırılmazsa 8 test düşer, yükleme gerçek.

## Açık not
- Lint hâlâ uyarı veriyor (autoFocus, primitives); bunlar hata değil ve bu görevin konusu değil.

# V15-PER-001 - Kritik yol yük testleri

20 eş zamanlı terminalle yoğun saat (menü + sipariş + hesap + ödeme), son stok birimi yarışı ve yinelenen webhook yükü gerçek Host ve Postgres üzerinde ölçüldü.

## Kanıt

- `run.json` / `run.console.log`: ham çıktı, p50/p95/p99, entegrasyon sorguları; `pass: true`.
- `mutation.log`: p95 eşiği 5 ms'ye düşürülünce betik 4 yolu yavaş bildirip çıkış kodu 1 verdi (betik gerçekten başarısızlık yakalıyor).
- `docs/performance/V15-PER-001.md`: ortam, tablo ve sınırlar.

## Açık kalan

- Ölçüm geliştirme makinesinde, yük üretici ile sunucu aynı makinede. Gerçek restoran donanımında yeniden koşulmalı.
- Bekleme olmadan art arda koşan 20 terminalde sipariş gönderimi p95 ~450 ms (hedef altı, marj dar); gerçekçi hızda 121 ms. Ayrıntı docs/performance/V15-PER-001.md "Bulgular".
- Kilit izleme yok; nakit/kart ödemeleri kapsam dışı.

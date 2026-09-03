# V1-RMD-093 - V1 critical path write load test

- Task ID: V1-RMD-093
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: validation
- Surface state: Existing

## Source basis

- PO:2026-09-01
- PDF:I.38
- PDF:I.45.1

## Goal

 1. dalga yalnızca okuma yolunu ölçmüştü. Bu görev yazma kritik yolunu — masa oturt (`POST /orders/table`), kalem ekle (`POST /orders/{id}/items`), sipariş gönder (`POST /orders/{id}/submit`) — production boyutlu veri (yaklaşık 1.000.000 sipariş + 3.000.000 kalem arka plan verisi, yüzlerce boş masa, yüzlerce ürün) üzerinde eş zamanlı terminal yükü altında ölçer ve çalışırken veritabanı kilit/deadlock davranışını gözlemler. Sonuç `V15-PER-001` hedefine (20 terminal, sipariş gönderiminde p95 < 500 ms, p99 < 1 s) göre değerlendirilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-093-v1-critical-path-write-load-test.md`
- `tools/load-test/load_test.py` içine yazma senaryosu ve `tools/load-test/seed-loadtest.sh` seed betiği.
- `docs/performance/critical-path-load-v1.md` sonuç raporu.
- `evidence/V1-RMD-093/**` altındaki ham çıktı, kilit istatistikleri ve tarihli sonuç.

## In scope

- `tools/load-test/seed-loadtest.sh`: çalışan `alkaros` veritabanına yüzlerce boş masa, yüzlerce satılabilir ürün ve `LT-` önekiyle etiketli yaklaşık 1M sipariş + 3M kalem arka plan verisi ekler; sonunda etiketli veriyi temizleyen bir komut sunar.
- `tools/load-test/load_test.py` `--scenario write`: her terminal ayrık bir masa dilimi üzerinde oturt → 1-3 kalem ekle → gönder döngüsü çalıştırır; RPS, hata yüzdesi ve gönderim işlemi için p50/p90/p95/p99 raporlar.
- Yük sırasında `pg_stat_activity` (`wait_event_type = 'Lock'`), `pg_stat_database.deadlocks` (öncesi/sonrası) ve PostgreSQL `log_lock_waits` kayıtlarının toplanması.
- `docs/performance/critical-path-load-v1.md`: yöntem, veri hacmi, ölçülen tablo, kilit/deadlock gözlemi, `V15-PER-001` hedefine göre değerlendirme ve sınırlamalar.

## Out of scope

- `tests/Performance/CriticalPaths/**` ve `docs/performance/V15-PER-001.md` (V15-PER-001 sahipliğinde).
- Ödeme akışı yükü; V1'de ödeme kapalıdır.
- Production kodunda performans ayarı; tespit edilen darboğazlar ayrı kusur olarak kaydedilir.
- Çoklu düğüm ve uzun soak.

## Dependencies

- V1-GOV-062

## Deliverables

- `tools/load-test/seed-loadtest.sh` ve güncellenmiş `tools/load-test/load_test.py`.
- `docs/performance/critical-path-load-v1.md`.
- `evidence/V1-RMD-093/` altında ham tarama çıktısı, kilit istatistikleri ve sonuç özeti.

## Acceptance evidence

- Yazma senaryosu 1M satır arka plan verisi üzerinde eş zamanlı terminal taramasıyla çalışır ve sipariş gönderimi için p50/p90/p95/p99 yayınlar.
- `V15-PER-001` hedefine göre açık geçer/geçmez değerlendirmesi ve gözlemlenen kilit/deadlock davranışı `docs/performance/critical-path-load-v1.md` altında yazılıdır.
- Etiketli seed verisi çalıştırma sonrası temizlenir; kalıcı şema değişikliği yoktur.

## Handoff

- V1-GOV-063

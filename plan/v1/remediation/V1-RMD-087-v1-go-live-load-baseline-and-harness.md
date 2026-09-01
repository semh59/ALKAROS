# V1-RMD-087 - V1 go-live load baseline and harness

- Task ID: V1-RMD-087
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-09-01
- PDF:I.38
- PDF:I.45.1

## Goal

V1 çekirdeğinin (menü görüntüleme, masa taslağı, sipariş gönderimi, aktif sipariş yoklaması) eş zamanlı yük altındaki gecikme ve hata davranışı ölçülmemiştir. Bu görev sıfır bağımlılıklı tekrarlanabilir bir yük testi harness'i, kritik yol iş yükü modeli, 1..20 eş zamanlı terminal taraması ve ölçülen p50/p90/p95/p99 değerleri ile bir go-live temel raporu ekler. Tam kritik yol yük testi (`V15-PER-001`) ayrı kalır; bu görev ona girdi üretir.

## Owned surface

- `plan/v1/remediation/V1-RMD-087-v1-go-live-load-baseline-and-harness.md`
- `tools/load-test/**` altındaki harness ve çalıştırma sarmalayıcısı.
- `docs/performance/load-baseline-v1.md` temel raporu.
- `evidence/V1-RMD-087/**` altındaki ham çıktı ve tarihli sonuç.

## In scope

- `http.client` ve `concurrent.futures` ile yazılmış, hedef URL, kimlik doğrulama bootstrap'ı, senaryo, eş zamanlılık ve süre/istek sayısı alan `tools/load-test/load_test.py`.
- Ağırlıklı kritik yol senaryosu: menü/katalog okuma, masa taslağı yazma ve aktif sipariş yoklaması.
- `X-Forwarded-Proto: https` başlığı ile güvenilir proxy arkasındaki Host'a çalışan istekler ve `/api/v1/auth/login` üzerinden oturum çerezi elde eden bootstrap.
- 1, 2, 5, 10, 20 eş zamanlılıkta tarama; her satır için RPS, hata yüzdesi ve p50/p90/p95/p99/max gecikme.
- `docs/performance/load-baseline-v1.md`: yöntem, iş yükü modeli, ölçülen tablo, `V15-PER-001` hedefine (20 terminal, p95 < 500 ms, p99 < 1 s) göre değerlendirme ve sınırlamalar.

## Out of scope

- Production kodunda performans ayarı veya yeni özellik; tespit edilen darboğazlar ayrı kusur olarak kaydedilir.
- `V15-PER-001` owned surface'i (`tests/Performance/CriticalPaths/**`, `docs/performance/V15-PER-001.md`); bu görev oraya yazmaz.
- Ödeme akışı yükü; V1'de ödeme kapalıdır.

## Dependencies

- V1-GOV-050

## Deliverables

- `tools/load-test/load_test.py` ve `tools/load-test/run-baseline.sh`.
- `docs/performance/load-baseline-v1.md` temel raporu.
- `evidence/V1-RMD-087/` altında ham tarama çıktısı ve tarihli sonuç özeti.

## Acceptance evidence

- Harness çalışan ALKAROS yığınına karşı tekrarlanabilir biçimde çalışır ve her eş zamanlılık seviyesi için p50/p90/p95/p99 yayınlar.
- 20 eş zamanlı terminal taraması ölçülür; sonuç `V15-PER-001` hedefine göre açıkça değerlendirilir (geçer/geçmez ve gözlemlenen darboğaz).
- Ham çıktı ve komut satırı `evidence/V1-RMD-087/` altına yazılır.

## Handoff

- V1-GOV-051

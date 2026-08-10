# V15-PER-001 - Implement critical-path load tests

- Task ID: V15-PER-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.38
- PDF:I.45.1

## Goal

Tanımlanan eş zamanlılık kapsamında order gönderimini, son bölüm rezervasyonunu, payment kapanışını ve webhook alımını
ölçün.

## Owned surface

- `tests/Performance/CriticalPaths/**`, `docs/performance/V15-PER-001.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- İş yükü modeli, gecikme yüzdesi, aktarım hızı, veritabanı kilitleri ve kaynak sınırları.

## Out of scope

- Production ayar değişiklikleri, ayrı olarak kaydedilen kusurlar dışında.

## Dependencies

- GATE-V14-EXIT

## Deliverables

- `tests/Performance/CriticalPaths/**` altında Goal kapsamını çalıştıran validation asset, raw output ve tarihli result.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tekrarlanabilir çalışma ortamı ve p50/p95/p99'u yayınlar; Yük altında yinelenen/negatif/yanlış mali durum yok.
- Hedef: 20 eş zamanlı terminal altında kritik yollarda p95 < 500 ms, p99 < 1 s; yük profili: yoğun saat senaryosu
  (menü görüntüleme + sipariş gönderimi + ödeme akışı).

## Handoff

- V20-GAT-002

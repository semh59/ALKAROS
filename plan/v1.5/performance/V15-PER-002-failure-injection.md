# V15-PER-002 - Implement failure-injection test suite

- Task ID: V15-PER-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.38
- PDF:I.45.1

## Goal

Kritik işlem sınırlarında süreç, veritabanı, ağ, provider ve yazıcı hatalarını enjekte edin.

## Owned surface

- `tests/Resilience/FailureInjection/**`, `docs/resilience/V15-PER-002.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kilitlenme pencereleri, timeout, yeniden bağlanma, retry fırtınası, disk dolu ve kurtarma değişmezleri.

## Out of scope

- Halihazırda başka bir göreve ait olmayan yeni production kurtarma davranışı.

## Dependencies

- V15-PER-001
- V15-BKP-002
- V15-OBS-002

## Deliverables

- `tests/Resilience/FailureInjection/**` altında Goal kapsamını çalıştıran validation asset, raw output ve tarihli
  result.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Her enjekte edilen başarısızlığın deterministik güvenli sonucu vardır; sessiz başarı yok, kayıp order, yinelenen mali
  etki veya negatif stok.
- `V15-OBS-002` kanıtlı `NotApplicable` ise alert kaynaklı failure enjeksiyon senaryoları beklenmez; kalan
  başarısızlıklar için deterministik güvenli sonuç kuralı yine geçerlidir.

## Handoff

- V20-GAT-002
- V20-DRL-001

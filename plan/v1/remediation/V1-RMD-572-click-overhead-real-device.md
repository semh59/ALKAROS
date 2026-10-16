# V1-RMD-572 - Tıklama ek yükü: gerçek kasa cihazı ölçümü

- Task ID: V1-RMD-572
- Status: Blocked
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

Tıklama etkileşim kaydının gerçek kasa cihazındaki ek yükü ölçülür ve p95 değeri 5 ms altında kanıtlanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-572-click-overhead-real-device.md`
- `evidence/V1-RMD-572/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Gerçek kasa cihazında, kayıt açık ve kapalı iken tıklama işleme süresinin ölçülmesi.

## Out of scope

- Ölçüm betiği dışında kod değişikliği.

## Dependencies

- V1-RMD-541
- V1-RMD-553

## Blocker

- Ölçüm gerçek kasa cihazı ister; bu oturumda cihaza erişim yok. Görev ancak gerçek kasa cihazına erişim sağlandığında açılır; o zaman ölçüm yapılır. Bu görev V1-RMD-541'den V1-RMD-556'ya, oradan buraya taşındı.

## Acceptance evidence

1. Tıklama ek yükü gerçek kasa cihazında p95 5 ms altında.
2. Çıktılar `evidence/V1-RMD-572/` altındadır.

## Handoff

- None

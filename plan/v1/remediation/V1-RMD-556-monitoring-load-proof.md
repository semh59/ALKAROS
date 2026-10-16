# V1-RMD-556 - İzleme yükü ve fırtına ölçümü

- Task ID: V1-RMD-556
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

İzleme sisteminin kasa terminaline ve veritabanına yükü ölçülür; olay fırtınasında davranışı kanıtlanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-556-monitoring-load-proof.md`
- `evidence/V1-RMD-556/**`
- `evidence/V1-RMD-556/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Gerçek kasa cihazında ve Docker yığınında yük ölçümü; fırtına enjeksiyonu.
- Tıklama etkileşim kaydının gerçek kasa cihazındaki ek yükü V1-RMD-572'ye taşındı (gerçek cihaz gerekir).
- Docker yığınında `Monitoring:Enabled` açık/kapalı dönüşümlü turlarla sipariş onayı p95 karşılaştırması (kapalı tur: `Monitoring__Enabled=false` ortam değişkeni, yalnız api yeniden başlatılır); fırtına testi V1-RMD-571'in test projesinde Docker Postgres'e karşı çalıştırılır (HTTP uçlarının oturum başına kotası fırtınayı bilerek engeller).

## Out of scope

- Yok.

## Dependencies

- V1-RMD-534
- V1-RMD-541
- V1-RMD-543
- V1-RMD-571
- V1-RMD-573
- V1-RMD-574

## Acceptance evidence

1. İzleme açıkken sipariş onayı p95 süresi kapalıya göre en çok %10 yavaş.
2. Saniyede 1000 olay fırtınasında ana havuz bağlantı kullanımı değişmez (Docker Postgres'e karşı, V1-RMD-571 testiyle).
3. Çıktılar `evidence/V1-RMD-556/` altındadır.

## Handoff

- None

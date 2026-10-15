# V1-RMD-542 - İzleme kimliği zinciri

- Task ID: V1-RMD-542
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Bir düğmeye basıldığında başlayan izleme kimliği (trace id) isteğe, veritabanı komutuna, olaylara ve denetim kaydına taşınır; tek bir eylemin bütün yolu tek kimlikle görülür.

## Owned surface

- `plan/v1/remediation/V1-RMD-542-trace-correlation.md`
- `evidence/V1-RMD-542/**`
- `src/Host/Observability/TraceCorrelation/**`
- `tests/Host/Observability/TraceCorrelation/**`
- `src/Clients/PosTerminal/src/features/trace-correlation/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- İstemci her API çağrısına W3C `traceparent` başlığı ekler; sunucu `Activity` açar ve kimliği günlüğe, hata olayına ve denetim kaydına yazar.
- Veritabanı komutları ve dış çağrılar (Yemeksepeti, QNB) alt aralık (span) olarak süreleriyle kaydedilir.

## Out of scope

- Dış izleme sunucusu (Tempo/Jaeger) kurulumu.

## Dependencies

- V1-RMD-533

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-542/` altındadır.

## Handoff

- None

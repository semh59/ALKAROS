# V1-RMD-549 - İzleme verisi gizliliği ve saklama

- Task ID: V1-RMD-549
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

İzleme verisi kişisel veri, ödeme verisi veya girilen metin taşımaz; saklama süreleri tanımlıdır ve süresi dolan veri silinir.

## Owned surface

- `plan/v1/remediation/V1-RMD-549-telemetry-privacy-retention.md`
- `evidence/V1-RMD-549/**`
- `src/Modules/Observability/TelemetryPrivacy/**`
- `tests/Modules/Observability/TelemetryPrivacy/**`
- `database/migrations/V1/V1-RMD-549/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- İzin verilen alanların listesi (allowlist); listede olmayan alan reddedilir; sızıntı testi.
- Saklama: ham olay 30 gün, özetler 13 ay; silme işi ve denetim kaydı.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-541

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-549/` altındadır.

## Handoff

- None

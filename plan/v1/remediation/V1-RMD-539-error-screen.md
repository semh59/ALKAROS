# V1-RMD-539 - Yönetim ekranı: sistem sağlığı ve hatalar

- Task ID: V1-RMD-539
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Müdür, yönetim ekranında canlı hata listesini görür; süzer, ayrıntı ve tekrar sayısına bakar, hatayı onaylar veya çözüldü işaretler.

## Owned surface

- `plan/v1/remediation/V1-RMD-539-error-screen.md`
- `evidence/V1-RMD-539/**`
- `src/Clients/PosTerminal/src/features/management-error-events/**`
- `src/Host/Observability/ErrorEventEndpoints/**`
- `tests/Host/Observability/ErrorEventEndpoints/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Liste, katman/şiddet/durum süzgeci, ayrıntı, onayla, çözüldü, dedektör sağlığı şeridi.
- Tüm metin Türkçe; ham hata metni ekrana doğrudan basılmaz (UI_STYLE_GUIDE).

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533
- V1-RMD-536

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-539/` altındadır.

## Handoff

- None

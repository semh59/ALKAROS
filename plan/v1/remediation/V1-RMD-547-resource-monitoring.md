# V1-RMD-547 - Kaynak izleme

- Task ID: V1-RMD-547
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Konteyner ve sunucu kaynakları (işlemci, bellek, disk, ağ) ile gerçek zamanlı bağlantı (SignalR) kopmaları ve garson çevrimdışı kuyruk uzunluğu izlenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-547-resource-monitoring.md`
- `evidence/V1-RMD-547/**`
- `src/Host/Observability/ResourceProbes/**`
- `tests/Host/Observability/ResourceProbes/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Disk dolması, bellek baskısı, bağlantı kopma sıklığı ve çevrimdışı kuyruk birikmesi için eşikler ve olaylar.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-547/` altındadır.

## Handoff

- None

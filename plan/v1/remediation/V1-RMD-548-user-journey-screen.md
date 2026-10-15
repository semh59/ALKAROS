# V1-RMD-548 - Kullanıcı yolculuğu ekranı

- Task ID: V1-RMD-548
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Müdür, bir eylemin başından sonuna zaman çizelgesini görür: düğme, istek, veritabanı, olay; her adımın süresi ve en yavaş eylemler listesi.

## Owned surface

- `plan/v1/remediation/V1-RMD-548-user-journey-screen.md`
- `evidence/V1-RMD-548/**`
- `src/Clients/PosTerminal/src/features/management-user-journeys/**`
- `src/Host/Observability/JourneyEndpoints/**`
- `tests/Host/Observability/JourneyEndpoints/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Eylem listesi (en yavaş, en hatalı), tek eylem zaman çizelgesi, rol ve terminal süzgeci.
- Tüm metin Türkçe; teknik terimler ekranda açıklamalıdır.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-541
- V1-RMD-542
- V1-RMD-543

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-548/` altındadır.

## Handoff

- None

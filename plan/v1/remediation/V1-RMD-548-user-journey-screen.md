# V1-RMD-548 - Kullanıcı yolculuğu ekranı

- Task ID: V1-RMD-548
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Müdür bir eylemin zaman çizelgesini görür: düğme, istek, veritabanı, olay; her adımın süresi ve en yavaş eylemler.

## Owned surface

- `plan/v1/remediation/V1-RMD-548-user-journey-screen.md`
- `evidence/V1-RMD-548/**`
- `src/Clients/PosTerminal/src/features/management-user-journeys/**`
- `src/Host/Observability/JourneyEndpoints/**`
- `tests/Host/Observability/JourneyEndpoints/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Eylem listesi (en yavaş, en hatalı), tek eylem zaman çizelgesi (sunucu saatine göre), rol ve terminal süzgeci.
- Personel hareketleri ayrı, daha dar bir yetkiyle (vardiya sorumlusuna açık değil); teknik terimler ekranda açıklanır.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-541
- V1-RMD-542
- V1-RMD-543

## Acceptance evidence

1. Seçilen eylemin tüm adımları tek izleme kimliğiyle sıralı görünür.
2. Yetkisiz rol 403 alır.
3. Çıktılar `evidence/V1-RMD-548/` altındadır.

## Handoff

- None

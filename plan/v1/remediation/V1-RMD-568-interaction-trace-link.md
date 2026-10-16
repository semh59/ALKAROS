# V1-RMD-568 - Tıklamayı izleme kimliğine bağlama

- Task ID: V1-RMD-568
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

Bir düğme tıklaması, o tıklamanın başlattığı isteğin izleme kimliğiyle kayıtlanır; kullanıcı yolculuğu zaman çizelgesi düğmeyi ilk adım olarak gösterir.

## Owned surface

- `plan/v1/remediation/V1-RMD-568-interaction-trace-link.md`
- `evidence/V1-RMD-568/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/interaction-telemetry/** - yalnız izleme kimliği alanı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/InteractionTelemetry/** - yalnız izleme kimliği alanı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/InteractionTelemetry/** - yalnız izleme kimliği testi
- `database/migrations/V1/V1-RMD-568/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Etkileşim kaydına isteğe bağlı izleme kimliği alanı; istemci, tıklamadan sonraki ilk API isteğinin izleme kimliğini kayda ekler.
- Kullanıcı yolculuğu uç noktasında düğme adımının bu kimlikle eşleştirilmesi.

## Out of scope

- Tıklamaya bağlanamayan istekler (arka plan yoklamaları) için düğme adımı üretmek.

## Dependencies

- V1-RMD-541
- V1-RMD-548

## Acceptance evidence

1. Bir tıklamanın başlattığı istek, aynı izleme kimliğiyle tıklama kaydına bağlanır.
2. Bağlanamayan tıklama izleme kimliği olmadan kaydedilir ve reddedilmez.
3. Çıktılar `evidence/V1-RMD-568/` altındadır.

## Handoff

- None

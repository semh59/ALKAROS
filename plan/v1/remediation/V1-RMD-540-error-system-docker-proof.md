# V1-RMD-540 - Hata yakalama sistemi uçtan uca kanıt

- Task ID: V1-RMD-540
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Docker yığınında enjekte edilen gerçek hatalar olay olarak görünür, bildirim kuyruğuna düşer, ekranda listelenir; etkileşim ve performans zinciri kanıtlanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-540-error-system-docker-proof.md`
- `evidence/V1-RMD-540/**`
- `evidence/V1-RMD-540/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yazıcı kopması, yavaş sorgu, istisna, tarayıcı hatası ve düğme etkileşimi enjekte edilir; her biri için olay, sayaç, bildirim ve zaman çizelgesi kaydı kanıtlanır.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-534
- V1-RMD-535
- V1-RMD-537
- V1-RMD-538
- V1-RMD-539
- V1-RMD-541
- V1-RMD-545
- V1-RMD-546
- V1-RMD-547
- V1-RMD-548
- V1-RMD-552
- V1-RMD-553
- V1-RMD-554
- V1-RMD-556

## Acceptance evidence

1. Her kabul koşulu için Docker çıktısı ve ekran görüntüsü kanıtı.
2. Semih'in elle deneyebileceği senaryo listesi README'de.
3. Çıktılar `evidence/V1-RMD-540/` altındadır.

## Handoff

- None

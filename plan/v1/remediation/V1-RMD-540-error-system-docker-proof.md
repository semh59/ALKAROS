# V1-RMD-540 - Hata yakalama sistemi uçtan uca kanıt

- Task ID: V1-RMD-540
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Docker yığınında enjekte edilen gerçek hatalar olay olarak görünür, bildirim kuyruğuna düşer ve ekranda listelenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-540-error-system-docker-proof.md`
- `evidence/V1-RMD-540/**`
- `evidence/V1-RMD-540/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Yazıcı kopması, yavaş sorgu, istisna ve tarayıcı hatası enjekte edilir; her biri için olay, sayaç ve bildirim kaydı kanıtlanır.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-534
- V1-RMD-535
- V1-RMD-537
- V1-RMD-538
- V1-RMD-539

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-540/` altındadır.

## Handoff

- None

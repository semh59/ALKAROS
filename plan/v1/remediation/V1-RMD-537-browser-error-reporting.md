# V1-RMD-537 - Tarayıcı hata raporlama

- Task ID: V1-RMD-537
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

PosTerminal, garson ve kasa tarayıcı uygulamalarındaki yakalanmamış hatalar ve ağ hataları sunucuya raporlanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-537-browser-error-reporting.md`
- `evidence/V1-RMD-537/**`
- `src/Clients/PosTerminal/src/features/error-reporting/**`
- `src/Host/Observability/BrowserErrors/**`
- `tests/Host/Observability/BrowserErrors/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Bir uç nokta (oran sınırlı, en çok 200/saat), istemci tarafında aynı hatadan en çok 3 rapor, `sendBeacon`.
- Kişisel veri ve token içermez; metin uzunluğu sınırlıdır.

## Out of scope

- Ekran.

## Dependencies

- V1-RMD-533

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-537/` altındadır.

## Handoff

- None

# V1-RMD-567 - PosTerminal düğmelerine telemetri anahtarı

- Task ID: V1-RMD-567
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

PosTerminal'deki her düğme sitesi `data-telemetry-id` taşır; etkileşim kaydı otomatik anahtara düşmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-567-button-telemetry-keys.md`
- `evidence/V1-RMD-567/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Düğme sitesi taşıyan her kaynak dosyaya yalnız `data-telemetry-id` özniteliği eklenir (başlangıç commit'inde dosya listesi Sınırlı ek olarak yazılır); V1-RMD-541 statik taraması sıfır anahtarsız düğmeyle geçer.

## Out of scope

- Düğme davranışı, metni ve görünümü değişmez.

## Dependencies

- V1-RMD-541

## Acceptance evidence

1. Statik tarama anahtarsız düğme sitesi sayısını 0 bildirir.
2. Mevcut ekran testleri değişmeden geçer.
3. Çıktılar `evidence/V1-RMD-567/` altındadır.

## Handoff

- None

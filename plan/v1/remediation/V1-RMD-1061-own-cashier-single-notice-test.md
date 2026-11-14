# V1-RMD-1061 - Satış ekranı tek bildirim testi bir görevin sahipliğinde

- Task ID: V1-RMD-1061
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1060 görevinin eklediği Cashier.single-notice.test.tsx dosyası görev sahipliğinde kayıtlı değildi (plan denetimi UNOWNED_PRODUCTION_FILE verir). Dosya bu görevin sahipliğine alınır; içerik değişmez.

## Owned surface

- `evidence/V1-RMD-1061/**`
- `src/Clients/PosTerminal/src/routes/Cashier.single-notice.test.tsx`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Dosyanın sahiplik kaydı (içerik V1-RMD-1060 görevinde test edilip incelenmiştir)

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1060

## Acceptance evidence

1. plan-audit validate çıktısı (0 hata, 0 uyarı)
2. Dosyanın testlerinin geçmesi
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

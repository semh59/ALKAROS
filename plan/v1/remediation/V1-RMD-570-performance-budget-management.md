# V1-RMD-570 - Performans bütçelerini yönetimden ayarlama

- Task ID: V1-RMD-570
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

Performans bütçeleri bugün yalnız yapılandırma dosyasından okunur; yetkili kullanıcı bunları yönetim panelinden görür ve değiştirir.

## Owned surface

- `plan/v1/remediation/V1-RMD-570-performance-budget-management.md`
- `evidence/V1-RMD-570/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Bütçeleri listeleyen ve değiştiren, `observability.manage` yetkisi isteyen uç noktalar ve her değişiklik için denetim kaydı.
- Yönetim panelinde bütçe listesi ve düzenleme alanı (Türkçe metinler).

## Out of scope

- Yeni ölçüm türü eklemek.

## Dependencies

- V1-RMD-544
- V1-RMD-548

## Acceptance evidence

1. Yetkisiz rol 403 alır; değişiklik denetim kaydı bırakır.
2. Çıktılar `evidence/V1-RMD-570/` altındadır.

## Handoff

- None

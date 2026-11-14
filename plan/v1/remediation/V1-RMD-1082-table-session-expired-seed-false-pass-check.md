# V1-RMD-1082 - Masa yönetimi süresi dolmuş oturum testinin yanlış-geçme denetimi

- Task ID: V1-RMD-1082
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

TableManagementHttpTests.SessionAndCapabilityBoundariesReturn401And403 testindeki expired: true oturumunun gerçekten süre dolumu nedeniyle 401 ürettiği mutasyonla kanıtlanır (süre dolumu yoksayılırsa test kırılmalı). Üretim kodu değişmez; test yetersizse güçlendirilir.

## Owned surface

- `evidence/V1-RMD-1082/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/Tables/TableManagementHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Süre dolumu mutasyonu ve gerekirse test güçlendirmesi
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar
- Üretim kodu

## Dependencies

- V1-RMD-1081

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

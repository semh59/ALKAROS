# V1-RMD-951 - Yönetici onay kartları hangi hesap ve masa için olduğunu göstersin

- Task ID: V1-RMD-951
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu (G14): yönetici onay kartı yalnız yetki adı, tutar, gerekçe ve isteyen kişiyi gösteriyor; hangi hesap veya masa için olduğu yok, yönetici neyi onayladığını bilmeden karar veriyor. Karar: sunucu bekleyen onayın konusunu (hesap veya sipariş kalemi) Türkçe tek satırda çözer (Hesap H-1042 · Masa 5, Çay · Masa 5) ve kart bunu isteyen kişiden önce gösterir; konu çözülemezse satır çıkmaz.

## Owned surface

- `evidence/V1-RMD-951/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Authorization/AuthorizationDecisionContracts.cs
  - src/Host/Experience/Authorization/AuthorizationDecisionEndpoints.cs
  - tests/Host/Experience/Authorization/AuthorizationDecisionHttpTests.cs
  - src/Clients/PosTerminal/src/features/authorization-decisions/models.ts
  - src/Clients/PosTerminal/src/features/authorization-decisions/AuthorizationDecisionsWorkspace.tsx
  - src/Clients/PosTerminal/src/features/authorization-decisions/AuthorizationDecisionsWorkspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-950

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

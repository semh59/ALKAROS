# V1-RMD-952 - Yönetici onay düğmesi tek tıkla onaylamasın, onay adımı olsun

- Task ID: V1-RMD-952
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: yönetici onay kartında Onayla tek tıkla para kararını veriyor; kasiyer ekranındaki iki adımlı "Emin misiniz?" kalıbı burada yok. Karar: Onayla ilk tıkta kartı onay adımına çevirir (Emin misiniz? Evet, onayla / Vazgeç); istek ancak ikinci tıkta gönderilir. Reddet tek adım kalır (geri alınabilir bir ret değil ama para çıkışı yaratmaz).

## Owned surface

- `evidence/V1-RMD-952/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
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

- V1-RMD-951

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

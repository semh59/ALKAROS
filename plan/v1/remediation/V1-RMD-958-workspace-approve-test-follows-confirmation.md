# V1-RMD-958 - Yönetim çalışma alanı onay testi iki adımlı onayı izlesin

- Task ID: V1-RMD-958
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-952 Onayla düğmesini iki adımlı yaptı; routes/workspace.test.tsx içindeki onay-POST testi yalnız ilk tıkı atıyor ve kırıldı. Karar: test ikinci tıkı da (Evet, onayla) atar ve ilk tıkta POST gitmediğini de doğrular.

## Owned surface

- `evidence/V1-RMD-958/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-957

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

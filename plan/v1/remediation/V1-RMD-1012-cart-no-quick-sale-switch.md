# V1-RMD-1012 - Masa sepetinde Hızlı satışa geç düğmesi yok

- Task ID: V1-RMD-1012
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa siparişi sepetinde Hızlı satışa geç düğmesi vardı: masa hesabı ile hızlı satış arasında yanlışlıkla geçiş riski doğuruyor ve Masalar ekranındaki Hızlı satış düğmesiyle aynı işi ikinci kez yapıyordu. Düğme kaldırılır; hızlı satışa yalnız Masalar ekranından (temiz sepetle) geçilir.

## Owned surface

- `evidence/V1-RMD-1012/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.table-cart.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1011

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

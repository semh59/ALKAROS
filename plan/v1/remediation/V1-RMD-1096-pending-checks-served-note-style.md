# V1-RMD-1096 - Kasa kuyruğundaki "Yetkili yöneticiye başvurun" notu kasa arayüz kurallarına göre biçimlenir

- Task ID: V1-RMD-1096
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1093 ile eklenen pending-checks__served-note notunun CSS'i yoktur; not kartın sol kenarına yapışık ve genel meta renginde görünür. docs/CASHIER_UI_RULES.md'ye göre not 14 px (0.875rem) yazı, kenarlardan ve alttan en az 16 px (--ds-space-4) boşluk ve renk tablosundaki muted jetonu (--ds-color-muted) ile tanımlanır. Bir test bu üç kuralı CSS dosyasından okuyup sınar.

## Owned surface

- `evidence/V1-RMD-1096/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/pending-checks/pending-checks.css
  - src/Clients/PosTerminal/src/touchTargets.test.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Notun metni ve gösterim koşulu; diğer kasa kuyruğu stilleri

## Dependencies

- V1-RMD-1095

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

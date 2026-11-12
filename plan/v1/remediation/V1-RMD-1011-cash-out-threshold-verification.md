# V1-RMD-1011 - Nakit çıkış onay eşiği doğrulaması (₺400 onaysız kaydı)

- Task ID: V1-RMD-1011
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Deneme 4'te ₺400 nakit çıkışın onaysız kaydedilmesi, onay eşiği ayarına göre doğru davranış mı diye doğrulanır; sonuç kanıt olarak yazılır.

## Owned surface

- `evidence/V1-RMD-1011/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - evidence/V1-RMD-1011/RESULT.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1010

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

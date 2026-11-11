# V1-RMD-980 - Hesap acma ile mutfaksiz eklemenin yarisi ve yetersiz stok reddi

- Task ID: V1-RMD-980
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Hesap açılırken sipariş satırı kilitlenir; aynı anda yapılan mutfaksız ekleme ya faturaya girer ya da hesap açıldığı için reddedilir. Yetersiz stokta mutfaksız ekleme reddedilir ve kalem, stok ve denetim kaydı kalmaz.

## Owned surface

- `evidence/V1-RMD-980/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillingSplitStore.cs
  - tests/Host/Experience/Billing/KitchenlessAddHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-979

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

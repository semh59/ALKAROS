# V1-RMD-982 - Kasada eklendi isaretini hesap kalemlerinde ve gun sonu raporunda gostermek

- Task ID: V1-RMD-982
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Mutfaksız eklenen kalemin "kasada eklendi" işareti ve neden kodu hesap kalemlerinde görünür; gün sonu toplamlarında mutfaksız eklenen kalem sayısı ve tutarı ayrıca sayılır.

## Owned surface

- `evidence/V1-RMD-982/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillingSplitStore.cs
  - src/Host/Experience/Billing/BillingSplitContracts.cs
  - tests/Host/Experience/Billing/KitchenlessAddHttpTests.cs
  - src/Modules/Reporting/BusinessDayTotals/BusinessDayTotalsReader.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-981

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

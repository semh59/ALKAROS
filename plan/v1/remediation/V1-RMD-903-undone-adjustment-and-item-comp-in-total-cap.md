# V1-RMD-903 - Geri alınan indirim toplam sınırdan düşsün, garsonun kalem ikramı hesap toplamına girsin

- Task ID: V1-RMD-903
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Ürün sahibi kararı (Semih): geri alınan ikram/indirim hesap toplam sınırından DÜŞER; garsonun kalem ikramı hesap toplamına GİRER. V1-RMD-902 sonrası toplam, hesapta fiilen uygulanmış değerlerden okunduğu için geri alınan (silinen) düzeltme kendiliğinden düşer; kalem ikramı (bill_items.line_type = Complimentary) ise toplama eklenir. Bu görev ikisini gerçek Postgres testiyle kanıtlar ve toplam kaynağına kalem ikramını ekler.

## Owned surface

- `evidence/V1-RMD-903/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillAppliedAdjustmentTotals.cs
  - tests/Host/Experience/Billing/BillAdjustmentTotalCapUndoHttpTests.cs — yeni test
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Geri alma sonrası toplam düşer ve sınır yeniden açılır
2. Kalem ikramı toplama girer; sınırı aşan istek yönetici onayına düşer
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Kalem ikramının kendi kişisel bütçe tavanı, ayar ekranı

## Dependencies

- V1-RMD-902

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

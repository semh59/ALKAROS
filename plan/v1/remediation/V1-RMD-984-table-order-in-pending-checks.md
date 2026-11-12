# V1-RMD-984 - Hesabı alınmış masa siparişi Bekleyen hesaplar'da görünür

- Task ID: V1-RMD-984
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Dolu masanın siparişi hesap alınmadan Bekleyen hesaplar'da görünmez (mevcut davranış korunur); kasiyer Hesabı al deyip hesap açıldıktan sonra masaya bağlı sipariş de bu listede çıkar.

## Owned surface

- `evidence/V1-RMD-984/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/CashierHandoffStore.cs
  - tests/Host/Experience/Billing/MergedBillOrderLinkTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-983

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

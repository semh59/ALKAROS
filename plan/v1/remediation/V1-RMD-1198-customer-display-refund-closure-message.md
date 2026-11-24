# V1-RMD-1198 - Müşteri ekranı iade ile kapanan hesapta "Hesap iptal edildi" yazmaz

- Task ID: V1-RMD-1198
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 18 (bulgu 7): hesap iade ile kapandığında müşteri (ikinci) ekranı "Hesap iptal edildi." yazar; müşteri parasının geri verildiği bir işlemde bu yanlış okunur. `CustomerDisplayPaymentProgress` iptal edilmiş hesabın ödemelerine bakar: en az bir ödeme `Refunded` ise ekran boşa dönerken "İade tamamlandı." yazar; hiç iadesi olmayan gerçek iptalde mevcut "Hesap iptal edildi." metni kalır. Kasiyer ödeme sayfasındaki "İade Tamamlandı" başlığıyla aynı sözlük kullanılır (iade = para geri verildi, iptal = işlem hiç yapılmadı).

## Owned surface

- `evidence/V1-RMD-1198/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/CustomerDisplayPaymentProgress.cs
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayStatementTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (iade ile kapanan hesapta iade metni, gerçek iptalde eski metin, masa siparişi ve canlı hesap davranışı değişmez), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. İlgili Host testleri ve build exit code 0

## Out of scope

- İade tutarı toplamı (V1-RMD-1199); kasiyer ekranı; sunucu iade akışı

## Dependencies

- V1-RMD-1195

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

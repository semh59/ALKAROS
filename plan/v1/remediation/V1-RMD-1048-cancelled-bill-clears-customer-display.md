# V1-RMD-1048 - Hesap iptal edilince müşteri ekranı gönderilmemiş kalemli sipariş olsa da hemen boşalır

- Task ID: V1-RMD-1048
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Müşteri ekranı, iptal edilmiş hesabı olan siparişi yalnız siparişin düzenlenmediği (Editable=false) durumda boşaltıyordu; mutfağa gönderilmemiş kalemli sipariş düzenleniyor sayıldığı için hesap iptalinden sonra ekranda kalıyordu (V1-RMD-1041'de bulundu). Hesabı iptal edilmiş ve canlı hesabı kalmamış etkin sipariş artık düzenlenip düzenlenmediğine bakılmadan karşılama ekranına (Idle, "Hesap iptal edildi.") döner. İptal edilmiş hesabı olmayan, kasiyerin düzenlediği sipariş davranışı değişmez. Semih kararı 2026-11-13.

## Owned surface

- `evidence/V1-RMD-1048/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/CustomerDisplayPaymentProgress.cs
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayStatementTests.cs
  - tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1047

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

# V1-RMD-1097 - Servis edilmiş kalem kontrolü ile iptalin işlenmesi arasındaki yarış penceresi kapanır

- Task ID: V1-RMD-1097
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Hesap iptali ve masaya geri gönderme, servis edilmiş kalem kontrolünü yaptıktan sonra işlemi işlerken mutfak kalemi servis edebilir; hesap iptali siparişin satırını kilitlemediği için kontrol eskimiş veriyle geçer ve yetkisiz kasiyerin iptali servis edilmiş kalemli hesabı iptal edilmiş bırakır (masaya geri gönderme aynı pencereyi birleştirilmiş hesabın diğer siparişleri ve tekrar dalında taşır). Mutfak olay işleyicisi önce sipariş satırını günceller; bu yüzden kontrolden önce ilgili siparişlerin satırı aynı işlemde FOR UPDATE ile kilitlenir (mevcut kilit sırası: hesap kilidi, sonra sipariş satırı). Bekleyen mutfak güncellemesi kilidi bekler, işlem bitince görülür; kontrol ya güncel veriyi görür ya da güncelleme işlemden sonra gelir. Eşzamanlılık testi bekleyen mutfak güncellemesini taklit eder. Kasiyerin görünen davranışı, hata kodları ve yetkili yönetici akışı değişmez.

## Owned surface

- `evidence/V1-RMD-1097/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillCancellation.cs
  - src/Host/Experience/Orders/CashierHandoffStore.cs
  - tests/Host/Experience/Billing/BillCancellationHttpTests.cs
  - tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı eşzamanlılık testi, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Sipariş iptali (OrderCancellationStore, sürüm denetimiyle zaten korunur); mutfak olay işleyicisi; kilit sırasının genel yeniden tasarımı

## Dependencies

- V1-RMD-1096

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

# V1-RMD-1045 - Yönetici, onaylanmış ama fişi/havalesi girilmemiş iadeyi sebep göstererek iptal eder

- Task ID: V1-RMD-1045
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kart/havale iadesi onaylandıktan sonra fiş ya da havale bilgisi girilmezse istek "Elle iade bekliyor" durumunda takılır ve V1-RMD-1043 yüzünden hesap kapanamaz; çıkış yolu yoktu. Mevcut ret uç noktası ve durum makinesi (Rejected) bu durumu da kabul eder: payments.refund.approve yetkisi olan yönetici, zorunlu nedenle onaylı iadeyi iptal eder; istek Rejected olur, kapanış engeli kalkar, hesap yeniden kapanabilir. Kasa bütçesinden onaylanmış iade ise o vardiya hâlâ açıksa bütçeye geri yazılır. Denetim kaydı önceki durumu (AwaitingManualRefund) taşır. Tamamlanmış (Completed) ve reddedilmiş istek eskisi gibi 409 döner; kasiyer yetkisi olmayan çağrı 403 alır. Semih kararı 2026-11-13. Ekran işleri V1-RMD-1046 ve V1-RMD-1047'dedir.

## Owned surface

- `evidence/V1-RMD-1045/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/PaymentRefunds.cs
  - src/Modules/Payments/PaymentAggregate/PostgresRefundRequestRepository.cs
  - src/Modules/Cash/SessionLifecycle/PostgresRefundAllowanceRepository.cs
  - tests/Host/Experience/Billing/PaymentRefundHttpTests.cs
  - tests/Host/Experience/Billing/PaymentRefundCancelApprovedHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1044

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

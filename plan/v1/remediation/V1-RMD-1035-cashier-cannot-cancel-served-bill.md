# V1-RMD-1035 - Servis edilmiş ürünlü hesabı kasiyer iptal edemez; iptal bölümü nedenini ve iade yolunu açıkça yazar

- Task ID: V1-RMD-1035
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Servis edilmiş (müşteriye verilmiş) ürünü olan hesap yalnız iade onayı yetkisi (payments.refund.approve, yönetici) olan kullanıcı tarafından iptal edilebilir; kasiyerde sunucu 409 BILL_HAS_SERVED_ITEMS döner ve her iki iptal ekranı (Cashier split-payment, PosTerminal RefundPanel) sabit Türkçe cümleyle nedenini ve mevcut yolu söyler: ödeme alındıktan sonra "İade iste" ile yönetici onaylı iade. Yeni akış eklenmez. Aynı iptal bölümünde "Siparişi de iptal et" kutusunun açıklaması, işaretlenmezse ne olacağını da (yalnız hesap iptal olur, sipariş masada kalır, yeniden hesap açılır) ve işaretlenince geri alınamayacağını söyler; kutu varsayılan işaretsiz kalır. PO kararı 2026-11-13 (B).

## Owned surface

- `evidence/V1-RMD-1035/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillCancellation.cs
  - src/Host/Experience/Billing/BillingSplitApplication.cs
  - tests/Host/Experience/Billing/BillCancellationHttpTests.cs
  - tests/Host/Experience/Billing/MergedBillOrderLinkTests.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-cancel-order.test.js
  - src/Clients/PosTerminal/src/features/billing/refundApi.ts
  - src/Clients/PosTerminal/src/features/billing/refundApi.test.ts
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/features/billing/RefundPanel.cancel-order.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1034

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

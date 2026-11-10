# V1-RMD-901 - Yönetici, iptal ya da yanıt kaybıyla yanan orijinal fiş hakkını gerekçeyle sıfırlayabilsin

- Task ID: V1-RMD-901
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Ürün sahibi kararı (Semih): fişin ilk basımı orijinal sayılır ve sunucuda kaydedilir (billing.bills.receipt_first_printed_at/_by). Yazdırma iptal edilir ya da yanıt kaybolursa hak yanar, sonraki basımlar KOPYA olur. Bir yönetici (payments.refund.approve yetkisi, mevcut iade onayı kalıbı) gerekçe girerek hakkı sıfırlayabilir: tarih ve basan temizlenir, sonraki basım yeniden orijinal olur. İşlem denetim kaydına gerekçe, önceki tarih ve basanla yazılır; ödenmemiş hesap ya da basılmamış fiş Türkçe hata verir. Cashier ödenmiş hesap ekranında yönetici için gerekçeli sıfırlama bölümü eklenir.

## Owned surface

- `evidence/V1-RMD-901/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.Receipt.cs
  - src/Modules/Billing/BillFoundation/IBillReceiptPrintRepository.cs
  - src/Modules/Billing/BillFoundation/PostgresBillReceiptPrintRepository.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
  - tests/Clients/StaticApps/split-payment-receipt-reset.test.js — yeni test
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Sıfırlama uç noktası (yetki, gerekçe, denetim kaydı, Türkçe hata), depo yöntemi
2. Cashier ekranında gerekçeli sıfırlama bölümü ve Türkçe hata sözlüğü
3. Gerçek Postgres ve istemci testleri, mutasyon, iki bağımsız qa incelemesi

## Out of scope

- Fiş yazdırma kararının kendisi (ilk basım orijinal), React kasa

## Dependencies

- V1-RMD-900

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

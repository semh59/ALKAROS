# V1-RMD-908 - Fiş orijinal hakkı sıfırlama: basanın kendi fişini sıfırlaması yasak, sıfırlama sayısı sınırlı

- Task ID: V1-RMD-908
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-901 açıkları (Semih kararı: kapat): yönetici bir hesabın orijinal fiş hakkını gerekçeyle sıfırlayabiliyor; (1) orijinali basan kullanıcı o hakkı kendisi sıfırlayabiliyor (tek kişilik onay), (2) bir hesapta kaç kez sıfırlanabileceği sınırsız. Karar: orijinali basan kişi hakkı kendisi sıfırlayamaz, başka bir yönetici gerekir (403 RECEIPT_RESET_SELF, Türkçe); bir hesabın orijinal hakkı en fazla 2 kez sıfırlanabilir, sayı denetim olaylarından (bill.receipt.original.reset) sayılır, üçüncüsü 409 RECEIPT_RESET_LIMIT ile reddedilir ve hak yanık kalır. Kasiyer ekranının hata sözlüğüne iki yeni kod Türkçe eklenir. Sınır sayısı (2) bu görevin varsayımıdır ve raporda ürün sahibine bildirilir.

## Owned surface

- `evidence/V1-RMD-908/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.Receipt.cs
  - tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-receipt-reset.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı testler: basanın kendi sıfırlaması reddedilir, başka yönetici sıfırlar; üçüncü sıfırlama reddedilir ve hak yanık kalır
2. Sunucu kuralları ve ekran hata sözlüğü, istemci testi
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Sayının ayar ekranından değiştirilmesi, sıfırlama sayacının kalıcı sütunu (denetim olayları sayılır)

## Dependencies

- V1-RMD-907

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

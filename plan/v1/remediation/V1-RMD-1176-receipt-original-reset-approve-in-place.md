# V1-RMD-1176 - Yerinde yönetici onayı: kasiyer ekranında fişin orijinal hakkını yöneticinin şifresiyle sıfırlama

- Task ID: V1-RMD-1176
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Fiş orijinal hakkını sıfırlama (`POST .../receipt/original-reset`) bugün çağıranın `payments.refund.approve` yetkisini ister; kasiyer ekranında bölüm yetkisiz kasiyere hiç görünmüyor. İstek gövdesine isteğe bağlı `ManagerUsername` + `ManagerPassword` eklenir. Alanlar yoksa davranış aynıdır. Alanlar varsa çağıran yalnız `payments.take` ister, yönetici `InPlaceManagerApproval` ile doğrulanır ve `payments.refund.approve` aranır; "orijinali basan kişi sıfırlayamaz" kuralı (RECEIPT_RESET_SELF) onaylayan yöneticiye uygulanır; sıfırlama sınırı ve gerekçe kuralı aynıdır; `bill.receipt.original.reset` denetim kaydının aktörü onaylayan yöneticidir ve kaydın sonrası durumuna isteyen kasiyer yazılır. İstemcide (split-payment.js) bölüm yetkisiz kasiyere de gerekçe + "Yönetici burada onaylasın" alanlarıyla görünür; yetkili kullanıcı için akış aynıdır; şifre alanda yaşar ve her denemeden sonra boşaltılır; 14 px / 48 px, Türkçe sabit hata cümleleri. Yönetici çağırandan farklı olmak zorundadır (InPlaceSelfApprovalException) ve orijinali basan kişi sıfırlayamaz kuralı yöneticiye uygulanır.

## Owned surface

- `evidence/V1-RMD-1176/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.Receipt.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
  - tests/Clients/StaticApps/split-payment-receipt-reset.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (sunucu HTTP + istemci), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. `tests/Clients/StaticApps` ilgili testleri, BillReceipt HTTP testleri ile ApiConventions testi exit code 0

## Out of scope

- Fiş basım (orijinal/kopya) kuralları, sıfırlama sınırının değeri; şema ve migration; PosTerminal ekranı

## Dependencies

- V1-RMD-1175

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. Plan audit ve consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

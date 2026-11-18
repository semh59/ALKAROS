# V1-RMD-1175 - Yerinde yönetici onayı: kasiyer ekranında iade talebini yöneticinin şifresiyle onaylama (talep ve onay ayrı kişi kalır)

- Task ID: V1-RMD-1175
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

İade talebi onayı bugün `payments.refund.approve` sahibi ayrı girişle yapılabiliyor; kasiyer (yalnız `payments.refund.request`) ekranda "başka bir yetkili onaylamalı" görüyor. `POST .../billing/refund-requests/{requestId}/approve` gövdeye isteğe bağlı `ManagerUsername` + `ManagerPassword` alır. Alanlar yoksa davranış aynıdır. Alanlar varsa çağıran `payments.refund.request` ister, yönetici `InPlaceManagerApproval` ile doğrulanır ve `payments.refund.approve` aranır; `PaymentRefundStore.ApproveAsync` onaylayan olarak yöneticiyi alır (denetim izi onaylayanı yazar). Bilinen karar korunur: iade talep eden ile onaylayan ayrı kişidir (talep eden = onaylayan ise "yönetici kendi talebini onaylayabilir" ayarı kapalıyken mevcut reddi aynen çalışır; bu görev o ayarı veya kuralı değiştirmez). İstemcide (split-payment.js) "Requested" iade satırında onaylama yetkisi olmayan kullanıcıya "Yönetici burada onaylasın" alanları gösterilir; yetkili için mevcut Onayla/Reddet aynen kalır; şifre alanda yaşar ve her denemeden sonra boşaltılır; 14 px / 48 px, Türkçe sabit hata cümleleri. Gövde parametresi boş olabilir (mevcut çağıranlar, PosTerminal dahil, gövdesiz POST eder). Çağıran talebi kendisi açmış olmalı ve yönetici çağırandan farklı olmalıdır (ayardan bağımsız).

## Owned surface

- `evidence/V1-RMD-1175/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillingSplitApplication.cs
  - src/Host/Experience/Billing/BillingSplitContracts.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Host/Experience/Billing/PaymentRefundHttpTests.cs
  - tests/Clients/StaticApps/split-payment-refund-approve-here.test.js
  - src/Host/Experience/Billing/PaymentRefunds.cs
  - tests/Host/Experience/Billing/RefundSelfApprovalHttpTests.cs
  - tests/Host/Experience/Billing/RefundTerminalSessionHttpTests.cs
  - tests/Clients/StaticApps/split-payment-refunds.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (sunucu HTTP + istemci), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. `tests/Clients/StaticApps` ilgili testleri, Billing ve Refund HTTP testleri ile ApiConventions testi exit code 0

## Out of scope

- İade tutarı, kalem ve bütçe kuralları; reddetme ve iade iptali; PosTerminal RefundPanel ekranı (yönetici kendi girişiyle onaylamaya devam eder); şema ve migration

## Dependencies

- V1-RMD-1174

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. Plan audit ve consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- V1-RMD-1176

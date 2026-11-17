# V1-RMD-1142 - Yerinde yönetici onayı kilit mesajları bekleme süresini söyler

- Task ID: V1-RMD-1142
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu 8): yönetici onayında üç hatalı denemeden sonra kilit 15 dakika sürüyor, kilitliyken doğru şifre de reddediliyor ve mesaj süreyi söylemiyor ("Biraz bekleyin ..."); kasiyer ne kadar bekleyeceğini bilmiyor. Güvenlik gevşetilmez (3 deneme, 15 dakika, hesap kilidi aynen kalır); yalnız iki cümle süreyi söyler.

1. `TOO_MANY_ATTEMPTS` (kasiyer ya da yönetici anahtarı 3 hatalı denemeyle durdu; pencere `InPlaceApprovalThrottle` içindeki 15 dakika): "Çok fazla hatalı deneme. En çok 15 dakika sonra tekrar deneyin ya da yönetici kendi ekranından onaylasın." ("En çok": pencere ilk hatadan sayılır, kalan süre 15 dakikayı aşmaz; kalan süre yanıta yazılmaz, böylece sözleşme değişmez.)
2. `MANAGER_LOCKED` (yönetici hesabı kimlik doğrulamasının kendi kilidinde; `AuthenticationService.DefaultLockoutDuration` 15 dakika): "Bu yönetici hesabı en çok 15 dakika kilitli; yönetici kendi ekranından onaylasın."
3. Cümleler sunucu eşlemesinde (`BillingSplitApplication.cs`) ve Hesap Ödeme istemcisinin hata sözlüğünde (`split-payment.js`) birebir aynıdır; sözlük kilit testi bu eşitliği zaten korur. Süre sabitleri (15 dakika) testle sabitlenir ki biri değişirse cümle de değişmek zorunda kalsın.

## Owned surface

- `evidence/V1-RMD-1142/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillingSplitApplication.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs
  - tests/Clients/StaticApps/split-payment-discount-pending.test.js
  - tests/Clients/StaticApps/error-dictionary-lock.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. Billing HTTP testleri ve istemci testleri (tests/Clients/StaticApps)

## Out of scope

- Kilit süresi, deneme sayısı ya da kilit mantığı; kalan sürenin yanıta yazılması; kasa kapanışındaki onay (V1-RMD-1143, V1-RMD-1144)

## Dependencies

- V1-RMD-1141

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- V1-RMD-1143

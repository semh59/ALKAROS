# V1-RMD-884 - İndirimli hesapta ürün bazında bölme: kalem indirim dağıtımıyla çalışır (V1-RMD-413 reddinin kaldırılması)

- Task ID: V1-RMD-884
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

İndirim uygulanmış hesapta ürün bazında bölme hâlâ 409 (`ITEM_SPLIT_ON_ADJUSTED_BILL`) veriyordu, çünkü kalemlerin kendi fiyatları toplamı borçlanılan tutara uymuyordu. Kalem bazlı indirim dağıtımı artık var (`AdjustmentCalculator.DiscountedLineAmounts`, iade ve KDV kırılımı aynı dağıtımı kullanır). Ürün bazında bölme bu dağıtımla çalışır: her kalem indirimli tutarıyla, KDV'si de düzeltilmiş KDV toplamının kalemlere en büyük kalıntı yöntemiyle dağıtımıyla bölünür; tüm kalemler dağıtılınca toplam, ödenecek tutara ve düzeltilmiş KDV'ye kuruşu kuruşuna eşittir. Ek ücret veya bahşiş eklenmiş hesapta ürün bazında bölme 409 kalır (ek ücret/bahşiş kalemlere ait değildir), hata metni buna göre daralır; ekran uyarısı yalnız bu durumda görünür. Eşit bölmede kuruş kalıntısının son kişide kalması (Semih kararı) testle sabitlenir.

## Owned surface

- `evidence/V1-RMD-884/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Billing/SplitDesign/SplitEngine.cs — kalem tutarları indirimli
  - src/Host/Experience/Billing/BillingSplitStore.cs — yalnız ek ücret/bahşişte ret
  - src/Host/Experience/Billing/BillingSplitApplication.cs — hata metni
  - tests/Modules/Billing/SplitDesign/SplitDesignDomainTests.cs
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.recovery.test.tsx
  - src/Clients/PosTerminal/src/features/billing/billingApi.ts
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js — hata metni
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. `SplitEngine.CreateItemSplit` indirimli kalem tutarı ve KDV dağıtımı; mağaza tarafında yalnız ek ücret/bahşişte ret
2. İndirimli hesapta ürün bazında bölme testi (kuruş kalıntılı), eşit bölmede kalıntının son kişide kalması testi
3. Ekran uyarısı ve hata metinleri
4. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Ek ücret/bahşişin kalemlere dağıtımı, kısmi ödeme sonrası bölme tasarımının bayatlaması, iade ve KDV kırılımı kodu

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

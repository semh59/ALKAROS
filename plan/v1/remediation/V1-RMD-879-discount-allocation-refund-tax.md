# V1-RMD-879 - Hesap indiriminin kalemlere dağıtılması: iade ve KDV kırılımı tam toplam (F0287)

- Task ID: V1-RMD-879
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

F0287: hesap (bill) indirimi kalemlere tutar oranında kuruş hassasiyetinde (en büyük kalıntı, mevcut BillMath.Apportion) tek yerde dağıtılır; kalem iadesi bu dağıtımdan fiyatlanır (kalemin tüm adetleri iade edilince tam payı, kuruş kaybı yok) ve KDV kırılımı aynı dağıtımdan beslenir. Karar: kuruş kalıntısı 'son kalem' yerine mevcut yardımcının en büyük kalıntı kuralıyla verilir (toplam aynı, hiçbir kalem payından bir kuruştan fazla sapmaz).

## Owned surface

- `evidence/V1-RMD-879/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Billing/Adjustments/AdjustmentCalculator.cs — indirimli kalem tutarları ve iade payı yardımcıları
  - src/Modules/Billing/Adjustments/TaxBreakdown.cs — KDV oranı brüt toplamı kalem dağıtımından
  - src/Host/Experience/Billing/PaymentRefunds.cs — kalem iadesi fiyatlaması
  - tests/Modules/Billing/Adjustments/AdjustmentsDomainTests.cs — dağıtım, iade payı ve KDV kırılımı testleri
  - tests/Host/Experience/Billing/PaymentRefundHttpTests.cs — indirimli hesapta kısmi iade testleri
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. AdjustmentCalculator'a indirimli kalem tutarları (indirim kalem brüt tutarlarına BillMath.Apportion ile dağıtılır) ve kümülatif iade payı hesabı
2. PaymentRefunds kalem iadesi bu yardımcılarla fiyatlanır (tüm kalemler iade edilince tam ödenen tutar)
3. TaxBreakdown oran başına brüt tutarı aynı dağıtımdan alır
4. Birim ve gerçek Postgres testleri, mutasyon

## Out of scope

- Kalem bazlı bölme (V1-RMD-413 reddi), iade kaydına KDV yazmak, F0099 ve F0457

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

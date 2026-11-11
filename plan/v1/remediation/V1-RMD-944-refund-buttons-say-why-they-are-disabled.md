# V1-RMD-944 - İade düğmeleri pasifken nedenini söylesin

- Task ID: V1-RMD-944
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-943 incelemesinden: ödeme ekranındaki "İade iste", "Reddet", "İadeyi tamamla" ve "Seçilen ürünleri iade et" düğmeleri gerekçe, fiş numarası, adet, ödeme veya stok seçimi eksikken sessizce pasif kalıyor. Karar: her pasif düğmenin altında eksik olanı söyleyen tek cümle çıkar, eksik tamamlanınca kaybolur; düğme cümleye aria-describedby ile bağlanır.

## Owned surface

- `evidence/V1-RMD-944/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-refunds.test.js
  - tests/Clients/StaticApps/split-payment-item-refund.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-943

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

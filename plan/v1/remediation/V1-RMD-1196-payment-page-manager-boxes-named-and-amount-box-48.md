# V1-RMD-1196 - Ödeme sayfasında yönetici kutuları adlandırılır, tutar kutusu 48 px ölçülür

- Task ID: V1-RMD-1196
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 18 (bulgu 5, 6): aynı ödeme sayfasında iki ayrı "Yönetici kullanıcı adı / şifresi" kutusu görünür (iade isteği onayı ve orijinal fişi sıfırlama; indirim onayı üçüncü yerdir) ve hangisinin neye ait olduğu belli değildir. "Ödeme tutarı" ve "Alınan para" kutuları 46 px ölçülür.

1. Yönetici kutuları birleştirilmez (her biri ayrı bir yetkiyi onaylar: iade onayı, fiş hakkı, indirim; ortak kutu yanlış işleme onay verir). Her kutu görünür bir başlık ("İade onayı için yönetici", "Fiş hakkı için yönetici", "İndirim onayı için yönetici") ve işlemi söyleyen erişilebilir adlar taşır; alan kimlikleri ve davranış değişmez.
2. 46 px: dış kutu (`<label>`, dokunma hedefi) `min-height: 48px` zaten sağlar; ölçülen 46 px, kutu kenarlığı (2 x 1,5 px) düşülmüş iç yazı alanıdır (V1-RMD-1145 canlı ölçümü, V1-RMD-1188 karar notu). Kenarlık kutu içine alınmadan çizilir (iç gölge), böylece iç yazı alanı da 48 px olur ve ölçüm iki biçimde de 48 verir. Gerçek Chromium ölçümü (`getBoundingClientRect` ve hesaplanmış stil) kanıt klasörüne yazılır; kaynak testi kuralı sabitler.

## Owned surface

- `evidence/V1-RMD-1196/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - tests/Clients/StaticApps/cashier-amount-field-label.test.js
  - tests/Clients/StaticApps/cashier-touch-targets.test.js
  - tests/Clients/StaticApps/split-payment-refund-approve-here.test.js
  - tests/Clients/StaticApps/split-payment-receipt-reset.test.js
  - tests/Clients/StaticApps/split-payment-discount-pending.test.js
  - tests/Clients/StaticApps/split-payment-approval-restore.test.js
  - tests/Clients/StaticApps/split-payment-comp.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (iki kutu aynı sayfada ayrı başlık ve ad; tutar kutusu kenarlığı iç yazı alanını küçültmez), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. StaticApps ilgili testler, canlı yükseklik ölçümü ve istemci üretim derlemesi

## Out of scope

- Kasa oturumu sayfası (`cash-session`) kutuları; "parola/şifre" terim birleştirme; iade yollarının sayısı; sunucu

## Dependencies

- V1-RMD-1195

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; canlı ölçüm çıktısı; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

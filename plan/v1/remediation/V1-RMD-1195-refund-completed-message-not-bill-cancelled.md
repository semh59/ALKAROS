# V1-RMD-1195 - İade tamamlanınca iade sonucu gösterilir, "Hesap İptal Edildi" gösterilmez

- Task ID: V1-RMD-1195
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 18 (bulgu 4): tam iade tamamlanınca (sunucu `billCancelled: true` döner) ödeme sayfası doğrudan "Hesap İptal Edildi" ekranına geçer; "iade tamamlandı" bildirimi, tutar ve yöntem notu görünmez. İptal ile iade karışır.

1. İadeden gelen kapanışta (iade isteği, onay, tamamlama; vardiya bütçesinden anında tamamlanan dahil) ayrı bir sonuç ekranı çizilir: başlık "İade Tamamlandı" (başarı simgesi), iade edilen tutar ve yönteme göre not (nakit: "Parayı müşteriye geri verin."; kart: "Kart iadesi banka POS cihazında yapıldı; iade fişi: <no>"; havale: "Havale iadesi kaydedildi."), hesabın iade sonrası kapandığı bir cümleyle.
2. Gerçek iptal (hesap iptali düğmesi) mevcut "Hesap İptal Edildi" ekranını aynen korur.
3. Onay bekleyen iade ("onaylandı, kart için fiş numarası yazın") ekranı değişmez; yalnız hesabı kapatan sonuç ekranı değişir.

## Owned surface

- `evidence/V1-RMD-1195/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - tests/Clients/StaticApps/split-payment-refunds.test.js
  - tests/Clients/StaticApps/split-payment-refund-approve-here.test.js
  - tests/Clients/StaticApps/split-payment-cancel-order.test.js
  - tests/Clients/StaticApps/split-payment-allowance.test.js
  - tests/Clients/StaticApps/split-payment-cashier-rules.test.js
  - tests/Clients/StaticApps/split-payment-external-card.test.js
  - tests/Clients/StaticApps/split-payment-failures.test.js
  - tests/Clients/StaticApps/split-payment-item-refund.test.js
  - tests/Clients/StaticApps/split-payment-item-refund-single-payment.test.js
  - tests/Clients/StaticApps/split-payment-open-refund-fold.test.js
  - tests/Clients/StaticApps/split-payment-open-refund-closed.test.js
  - tests/Clients/StaticApps/split-payment-paid-refund-fold.test.js
  - tests/Clients/StaticApps/split-payment-refund-rules.test.js
  - tests/Clients/StaticApps/split-payment-reasons.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (nakit, kart fiş numarasıyla, havale, bütçeden anında tamamlanan, yerinde yönetici onayı: iade ekranı; hesap iptali: eski ekran), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. StaticApps ilgili testler ve istemci üretim derlemesi

## Out of scope

- Sunucu iade akışı ve yanıt sözleşmesi (yanıt `Amount`, `Method`, `SlipNumber`, `BillCancelled` zaten döner); iade fişi yazdırma; kısmi iade metinleri

## Dependencies

- V1-RMD-1194

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

# V1-RMD-1200 - Kasa oturumu sayfasında yönetici kutuları adlandırılır, kutu kenarlığı ve giriş yüksekliği 48 px ölçülür

- Task ID: V1-RMD-1200
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1196'nın bıraktığı açık risk: kasa oturumu sayfasında (`cash-session`) da iki ayrı yönetici kutusu aynı belirsiz görünümü taşır (kasa çıkışı talebini yerinde onaylama; fark tolerans sınırını aşan kapanışta yönetici kullanıcı adı ve şifresi) ve tutar kutusu 1,5 px kenarlıkla iç alanı 48 px'in altına düşürür.

1. Kutular birleştirilmez (farklı yetkiler). Her kutu görünür başlık ("Kasa çıkışı onayı · Yönetici burada onaylasın", "Kasa kapatma onayı · Yönetici") ve işlemi söyleyen erişilebilir adlar taşır; alan kimlikleri ve davranış değişmez.
2. `.cs-amount-field` kenarlığı, ödeme sayfasındaki kutu gibi kutunun içine çizilir (iç gölge 1,5 px marka rengi); dış kutu ve iç yazı alanı aynı 48 px ölçülür. Diğer giriş kutuları (`.cs-input`) zaten 48 px `min-height` taşır; gerçek Chromium ölçümü (`getBoundingClientRect`) tutar kutusu ve giriş kutuları için kanıt klasörüne yazılır; kaynak testi kuralı sabitler.

## Owned surface

- `evidence/V1-RMD-1200/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.css
  - tests/Clients/StaticApps/cashier-amount-field-label.test.js
  - tests/Clients/StaticApps/cashier-touch-targets.test.js
  - tests/Clients/StaticApps/cash-session-movement.test.js
  - tests/Clients/StaticApps/cash-session-close-manager-approval.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. StaticApps ilgili testler, canlı yükseklik ölçümü ve istemci üretim derlemesi

## Out of scope

- Ödeme sayfası; "parola/şifre" terim birleştirme; sunucu

## Dependencies

- V1-RMD-1196

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; canlı ölçüm çıktısı; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

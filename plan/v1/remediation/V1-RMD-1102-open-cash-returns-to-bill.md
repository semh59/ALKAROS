# V1-RMD-1102 - Kasa açılınca kasiyer ödeme sayfasına döner

- Task ID: V1-RMD-1102
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu: ödeme sayfası kasa kapalıyken "Kasayı aç" ile kasa oturumu sayfasına gönderiyor ve dönüş yok; kasiyer hesabı yeniden bulmak zorunda. Ödeme sayfası bağlantıya returnTo=<ödeme sayfası adresi> ekler; kasa oturumu sayfası returnTo değerini yalnız aynı kaynak içi göreli yol ise (tek eğik çizgiyle başlar; çift eğik çizgi veya ters eğik çizgiyle başlamaz; şema içermez) kabul eder. Geçerliyse üstteki geri bağlantısı "Hesaba dön" olur ve kasa başarıyla açılınca sayfa o adrese döner. Geçersiz returnTo yok sayılır (açık yönlendirme açığı yoktur). Kasa kuralları ve sunucu sözleşmesi değişmez.

## Owned surface

- `evidence/V1-RMD-1102/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Clients/StaticApps/cash-session-return-to.test.js
  - tests/Clients/StaticApps/split-payment-cash-return-link.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Sunucu sözleşmesi; kasa açma kuralları; kapanış akışı

## Dependencies

- V1-RMD-1101

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

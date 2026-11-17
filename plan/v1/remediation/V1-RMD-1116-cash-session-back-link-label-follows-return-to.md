# V1-RMD-1116 - Kasa açılış sayfasında geri bağlantısı nereye gittiğini doğru söyler

- Task ID: V1-RMD-1116
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 13: kasa açılış sayfası returnTo ile açılınca geri bağlantısı her durumda "← Hesaba dön" yazıyor; returnTo /tables (Masalar) ise bağlantı Masalara gidiyor ama etiket yanıltıcı. returnTo yolu /tables ise etiket "← Masalara dön" olur; diğer geçerli returnTo (ödeme/hesap sayfası) için "← Hesaba dön" kalır; returnTo yoksa veya güvensizse "← Kasaya dön" kalır. returnTo doğrulaması ve açılınca dönüş davranışı değişmez.

## Owned surface

- `evidence/V1-RMD-1116/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Clients/StaticApps/cash-session-return-to.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. Statik uygulama testleri

## Out of scope

- returnTo güvenlik doğrulaması; kasa kuralları; sunucu sözleşmesi

## Dependencies

- V1-RMD-1115

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

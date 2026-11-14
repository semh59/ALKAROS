# V1-RMD-1068 - Tahsilat sayfasında Hesabı iptal et ve İade iste düğmeleri yıkıcı stilde çizilir

- Task ID: V1-RMD-1068
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

docs/CASHIER_UI_RULES.md 5 yıkıcı eylemi yüzey zemini, danger yazı ve danger kenarla ister. Kasiyer denemesi 8 Tahsilat sayfasındaki "Hesabı iptal et", "İade iste" ve "Seçilen ürünleri iade et" düğmelerinin nötr (gri çerçeve) çizildiğini buldu. Bu düğmeler yeni .sp-btn-destructive sınıfını alır (pasifken mevcut pasif görünüm); onay adımındaki dolgulu kırmızı onay düğmesi değişmez. İade isteği tek dokunuş olarak kalır; ona onay adımı eklemek ürün kararıdır ve bu görevin dışındadır.

## Owned surface

- `evidence/V1-RMD-1068/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - tests/Clients/StaticApps/split-payment-cashier-rules.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. tests/Clients/StaticApps tam koşusu

## Out of scope

- Listede olmayan ekranlar ve dosyalar; iade isteğine onay adımı eklemek

## Dependencies

- V1-RMD-1067

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

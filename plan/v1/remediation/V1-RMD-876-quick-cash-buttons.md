# V1-RMD-876 - Nakit tahsilatta hızlı banknot düğmeleri ve Tam tutar

- Task ID: V1-RMD-876
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Ürün kararı F0053: kasiyer nakit tahsilatta verilen parayı tek dokunuşla girer. Banknot düğmeleri (5, 10, 20, 50, 100, 200 TL) Alınan para alanını o tutara AYARLAR (üstüne eklemez; yanlış basışı düzeltmek için tekrar basmak veya alanı elle değiştirmek yeterli). Tam tutar düğmesi ödenecek tutarı (varsayılan olarak kalan) yazar. Para üstü mevcut önizleme ve sunucu yanıtıyla gösterilir. Yalnız Cashier vanilla split-payment ekranı nakit alınan para alanına sahiptir; PosTerminal React tarafında nakit alınan para alanı yoktur. Yeni uç nokta ve migration yok.

## Owned surface

- `evidence/V1-RMD-876/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js — düğme satırı ve tıklama işleyicisi
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css — düğme satırı stili (dokunmatik hedef)
  - tests/Clients/StaticApps/split-payment-quick-cash.test.js — hızlı nakit düğmeleri testi
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Alınan para alanının altında 6 banknot düğmesi ve Tam tutar düğmesi (yalnız Nakit seçiliyken)
2. Banknot düğmesi alanı ayarlar, ekleme yapmaz; Tam tutar tutar alanını yazar
3. Önizleme ve Ödemeyi Ekle düğmesi güncellenir, anahtar sıfırlanır
4. Türkçe aria-label, klavyeyle erişilebilir (düğme öğesi), dokunmatik hedef sınıfı

## Out of scope

- PosTerminal React kasa (nakit alınan para alanı yok); sunucu ve uç nokta değişikliği

## Dependencies

- None

## Acceptance evidence

1. kırmızı/yeşil test günlükleri
2. mutasyon günlükleri
3. RESULT.md ve REVIEWS.md
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

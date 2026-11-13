# V1-RMD-1057 - Ödeme ekranında tek başlık, nötr Kalan tutarı, tek birincil düğme rengi ve tek para biçimi olur

- Task ID: V1-RMD-1057
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Hesap Ödeme ekranında üç başlık üst üste duruyor (HESAP ÖDEME / Tahsilat / Masa M-03), normal kalan bakiye bordo kırmızıyla hata gibi görünüyor, Ödenen satırı Kalan'dan ve Hesap toplamı'ndan küçük çiziliyor, Ödemeyi Ekle ve Fişi yazdır lacivert iken diğer ekranlarda birincil düğme camgöbeği, hızlı nakit düğmeleri "5 ₺" yazarken diğer tutarlar "₺160,00" yazıyor ve yazılar 12-13 piksel. docs/CASHIER_UI_RULES.md'ye göre: üst etiket kalkar, başlık "Tahsilat" kalır ve masa adı onun altında tek bağlam satırı olur; Kalan tutar nötr (ink) renkte kalır ve tamamen ödenince success olur, Kalan hâlâ Hesap toplamı'ndan büyük çizilir; Ödenen, Hesap toplamı ile aynı boyda olur; birincil düğme camgöbeği dolgu ve ink yazıdır; hızlı nakit düğmeleri tek para biçimini kullanır; ödeme sayfasında yazılar en az 14 piksel olur (yazdırma çıktısı hariç).

## Owned surface

- `tests/Clients/StaticApps/split-payment-cashier-rules.test.js`
- `evidence/V1-RMD-1057/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - tests/Clients/StaticApps/split-payment-hierarchy.test.js
  - tests/Clients/StaticApps/split-payment-quick-cash.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. Cashier sayfaları için tests/Clients/StaticApps testleri

## Out of scope

- Listede olmayan ekranlar ve dosyalar; iade ve iptal formu (V1-RMD-1059)

## Dependencies

- V1-RMD-1056

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. tests/Clients/StaticApps tam koşusu exit code 0
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

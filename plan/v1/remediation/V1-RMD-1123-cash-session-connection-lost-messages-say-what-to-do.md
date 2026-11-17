# V1-RMD-1123 - Kasa oturumu bağlantı hataları ne olduğunu ve ne yapılacağını söyler

- Task ID: V1-RMD-1123
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 15: Kasa oturumu sayfasında giriş, kasa açma, sayım başlatma, sayım kaydetme, hareket özeti ve beklenen tutarı yeniden okuma hataları yalnız "Sunucuya ulaşılamadı." diyor; kasiyer işlemin kaydedilip kaydedilmediğini ve ne yapacağını bilmiyor. Her biri (kasa kapama ve nakit hareketi yollarındaki mevcut iyi mesajlar gibi) bağlantının kesildiğini, işlemin kaydedilmiş olabileceğini (yalnız yazma işlemlerinde) ve ne yapılacağını söyler. Sunucu sözleşmesi ve işlem akışları değişmez.

## Owned surface

- `evidence/V1-RMD-1123/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Clients/StaticApps/cash-session-connection-errors.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci testleri (tests/Clients/StaticApps)

## Out of scope

- Sunucu sözleşmesi; kasa kapama ve nakit hareketi hata metinleri (zaten açıklayıcı); başka sayfalar

## Dependencies

- V1-RMD-1120

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- None

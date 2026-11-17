# V1-RMD-1126 - Kasa oturumu: bağlantı hataları tek kalıpta

- Task ID: V1-RMD-1126
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 16: Kasa oturumu sayfasında bağlantı hataları üç ifadeyle ("Sunucuya ulaşılamadı", "Bağlantı koptu", "Bağlantı kesildi") söyleniyor. Hepsi tek kalıba çevrilir: "Bağlantı kesildi. <ne olduğu>; <ne yapılacağı>." (nakit hareketi onayı ve kararı, oturum/kasa durumu okuma, kasa kapama sonrası bildirim dahil). İşlem akışları değişmez.

## Owned surface

- `evidence/V1-RMD-1126/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Clients/StaticApps/cash-session-connection-errors.test.js
  - tests/Clients/StaticApps/cash-session-load-errors.test.js
  - tests/Clients/StaticApps/cash-session-movement.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci testleri (tests/Clients/StaticApps)

## Out of scope

- Sunucu sözleşmesi; yukarıda sayılmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1123

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- None

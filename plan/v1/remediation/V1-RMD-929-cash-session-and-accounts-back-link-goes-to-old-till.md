# V1-RMD-929 - Kasa oturumu ve cari sayfalarındaki "Kasaya dön" ana kasaya gitsin

- Task ID: V1-RMD-929
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi (G14, bulgu 9): kasa oturumu ve cari hesap sayfalarındaki "← Kasaya dön" bağlantısı eski "Hızlı Sipariş Girişi" ekranına (/cashier/index.html) götürüyor; ödeme sayfası ise ana kasaya (/) dönüyor. Karar: üç sayfa da ana kasaya (/) döner.

## Owned surface

- `evidence/V1-RMD-929/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/index.html
  - src/Clients/Cashier/wwwroot/payments/customer-accounts/customer-accounts.js
  - tests/Clients/StaticApps/cashier-ui-navigation.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, iki bağlantının düzeltilmesi, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Eski hızlı sipariş ekranının kaldırılması

## Dependencies

- V1-RMD-928

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

# V1-RMD-971 - Kapanışta 'açık hesap' sayısı, hesabı henüz açılmamış siparişleri de içerdiğini söylesin

- Task ID: V1-RMD-971
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: kapanış uyarısı ve gün sonu özeti 'açık hesap' diyor; sunucu bu sayıya henüz hesabı açılmamış gönderilmiş siparişleri de katıyor (CashCloseStore.UnsettledSql), kasiyer hesap ekranında göremediği bir sayı görüyor. Karar: etiket 'açık hesap veya sipariş' olur (kasa kapanış uyarısı ve yönetim gün sonu özeti).

## Owned surface

- `evidence/V1-RMD-971/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Clients/StaticApps/cash-session-close.test.js
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-970

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

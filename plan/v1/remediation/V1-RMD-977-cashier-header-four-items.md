# V1-RMD-977 - Kasiyer üst başlığı en çok 4 öğe

- Task ID: V1-RMD-977
- Status: Planned
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer ana ekranının üst başlığı en çok dört öğe taşımalı: Masalar ile Hızlı satış geçişi, Bekleyen hesaplar, Kasa durumu ve kullanıcı menüsü. Mutfak, Müşteri ekranı, Cari hesaplar, Müşteri ekranını eşleştir, Rezervasyon, Ürün menüsü, Yönetim ve Çıkış kullanıcı menüsünün (Diğer) içine girer. Sunucu hazır göstergesi yalnız bağlantı sorunu olduğunda bir uyarı olarak görünür.

## Owned surface

- `evidence/V1-RMD-977/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/PosTerminal/src/routes/Cashier.header.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.management-link.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.help-alerts.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.logout-clears-storage.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.low-items.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.session-ended-message.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-976

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

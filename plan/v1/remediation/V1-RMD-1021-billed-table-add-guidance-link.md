# V1-RMD-1021 - Ürün ekle pasifken hesap ekranına yönlendiren bağlantı

- Task ID: V1-RMD-1021
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Hesabı açılmış masada Ürün ekle pasifti ve açıklama metni Hesabı iptal et yolunu anlatıyor ama kasiyeri oraya götüren bir düğme yoktu. Açıklamanın altına, ödeme yetkisi olan kasiyer için tek dokunuşla hesap ekranını açan bağlantı eklenir.

## Owned surface

- `evidence/V1-RMD-1021/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/styles.css
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1020

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

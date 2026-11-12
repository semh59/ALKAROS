# V1-RMD-996 - Garson masa taslağı hesabı açık masaya ürün eklemeyi reddeder

- Task ID: V1-RMD-996
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Hesabı açılmış masanın siparişine garson masa taslağı yolundan ürün eklenip mutfağa gönderilemez; sunucu Türkçe net bir çakışma hatası verir. Aynı turda hesap işaretçisi temizlenirken masada kalan açık fatura yeniden seçilir ve kasiyer masa başlatma çakışma mesajı Türkçe olur.

## Owned surface

- `evidence/V1-RMD-996/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/TableDraft/TableDraftService.cs
  - src/Host/Experience/Orders/TableDraft/TableBillOpenException.cs
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs
  - src/Host/DualScreen/DualScreenStore.Orders.cs
  - src/Modules/Tables/TableRelease/PostgresTableReleasePort.cs
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftTestDatabase.cs
  - tests/Host/DualScreen/DualScreenStoreTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-995

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

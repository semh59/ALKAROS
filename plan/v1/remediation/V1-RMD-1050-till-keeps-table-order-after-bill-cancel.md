# V1-RMD-1050 - Hesabı iptal edilmiş masa siparişi kasa satış ekranında "Ürün ekle" ile kendi masasında açılır, Hızlı satışa düşmez

- Task ID: V1-RMD-1050
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Hesabı iptal edilmiş masa siparişinde kasiyer "Ürün ekle" deyince kasa satış ekranı masa yerine boş "Hızlı satış" gösterdi (7. kasiyer denemesi, M-01). Kök neden: kasanın kendi okuması (aktif sipariş uç noktası) müşteri ekranının "iptal edilmiş hesap = boş ekran" kuralını da uyguluyor, sipariş "Idle" ve satırsız dönüyor; satış ekranı Idle siparişi sepet saymayıp Hızlı satış açıyor. Kasanın aktif sipariş okuması masa siparişini hesabı iptal edilmiş olsa da gerçek satırlarıyla döndürür; iptal edilmiş hesaplı Hızlı satış siparişi ve müşteri ekranı davranışı (V1-RMD-1048) değişmez. Semih kararı 2026-11-14.

## Owned surface

- `evidence/V1-RMD-1050/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/CustomerDisplayPaymentProgress.cs
  - src/Host/DualScreen/DualScreenApplication.Endpoints.cs
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayStatementTests.cs
  - tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1049

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

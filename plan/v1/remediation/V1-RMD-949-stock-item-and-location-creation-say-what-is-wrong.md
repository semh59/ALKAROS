# V1-RMD-949 - Stok kalemi ve konumu oluştururken 400 hatası neyin yanlış olduğunu söylesin

- Task ID: V1-RMD-949
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu (G14): stok kalemi oluşturma boş kod, ad, birim ya da geçersiz tür gönderildiğinde yalnız "İstek doğrulanamadı." gösteriyor. Karar: sunucu eksik alanı Türkçe adıyla söyler (Kod, Ad, Takip birimi boş olamaz; Stok türü ve Konum türü geçersiz) ve ekrandaki bildirim sunucunun cümlesini zaten olduğu gibi gösterir.

## Owned surface

- `evidence/V1-RMD-949/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Inventory/StockMasterEndpoints.cs
  - tests/Host/Experience/Inventory/StockMasterHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-948

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

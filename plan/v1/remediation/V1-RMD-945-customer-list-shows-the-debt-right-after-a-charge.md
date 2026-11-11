# V1-RMD-945 - Cari müşteri listesi, hesaba yazmanın hemen ardından borcu göstersin (doğrulama)

- Task ID: V1-RMD-945
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu (G14): cari müşteri kartı "Borç ₺0,00 / Müşterinin borcu yok" gösterirken müşterinin ₺30 borcu vardı. Sunucu tarafı sorgulandı: bakiye, defter satırı eklenirken veritabanı tetikleyicisiyle aynı işlemde güncelleniyor (gecikmeli bir izdüşüm yok) ve liste uç noktası bu satırı okuyor. Karar: sunucuda hesaba yazma sonrası liste ve arama yanıtının borcu ve kalan limiti doğru verdiğini kilitleyen test eklenir; kart eski yüklenmiş sayfadan geliyorsa sayfa yenilenince doğru görünür.

## Owned surface

- `evidence/V1-RMD-945/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/CustomerAccounts/CustomerAccountsHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-944

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

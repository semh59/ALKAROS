# V1-RMD-693 - Hata kataloğu ve hata zarfı

- Task ID: V1-RMD-693
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-599 bulguları B021, B022, B024, B065, B083, B084, B085, B089, B092, B096, B132, B134 kapanır. Veritabanına ulaşılamaması 503 (Retry-After ile) olur; genel InvalidOperationException 409'a düşmez, alana özel hatalar kullanılır; gövdesiz 404 ve catch-all yanıtları standart zarf taşır; İngilizce durum adı ve yöntem adı kullanıcıya çıkmaz; tutar biçimi Türkçe kültürdedir; hata kodu sözlüğü tek yerdedir; katalog test boşlukları kapanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-693-error-catalog-and-envelope.md`
- `evidence/V1-RMD-693/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Errors/** - yalnız hata kataloğu ve işleyici
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/** - yalnız hata ve zarf kullanımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/** - yalnız hata zarfı kullanımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/** - yalnız hata kodu sözlüğü
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/api-contract-standard.md - yalnız hata kodu adı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-692-notifications-privacy-retention.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kataloğun veritabanı ve çakışma eşlemeleri, alan istisna türleri, gövdesiz yanıtlar, kullanıcıya görünen metinler, kültür, istemci sözlüğü, mutantları öldüren testler.

## Out of scope

- İki farklı hata zarfı biçiminin birleştirilmesi (bilinen, ayrı karar).

## Dependencies

- V1-RMD-692

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-693/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-694

# V1-RMD-712 - Rezervasyon ve masa sunucu düzeltmeleri

- Task ID: V1-RMD-712
- Status: Planned
- Assignee: unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulguları B013, B014, B015, B016, B026, B029 ve Düşük sunucu kalemleri kapanır. Zaman damgaları sunucu saatiyle yazılır; rezervasyon OrderId masaya ait olmalıdır; rezervasyon ad, telefon, saat ve bitiş alanlarını alır ve süresi dolunca otomatik sona erer; Occupied masada Reserve komutu sunulmaz; KASA-1 teknik masası listelerden ve işlemlerden çıkar; kat planı kaydı yerleşim sürümüyle çakışır; masa tutarı birleşik ve çok siparişli masada doğru toplanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-712-table-reservation-and-server-leftovers.md`
- `evidence/V1-RMD-712/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-601-tables-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Zaman damgası, rezervasyon alanları ve süre dolumu, komut listesi, KASA-1 filtresi, kat planı sürümü, masa tutarı.

## Out of scope

- İstemci ekranları (V1-RMD-713), veri kısıtları (V1-RMD-714).

## Dependencies

- V1-RMD-601

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-712/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-602

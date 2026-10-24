# V1-RMD-715 - Masa doküman sapmaları ve modül sınırı düzeltmeleri

- Task ID: V1-RMD-715
- Status: Done
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulguları B032 ve sınır kalemleri S4, S5, S6, S7 kapanır. Tables'ın ham audit INSERT'leri `IAuditEventStore` üzerinden yazılır, modül DI kaydı modüle taşınır, Payments okuması modül bağımlılık dokümanına yazılır, ham SQL okuma/yazma kenarı için mimari test eklenir; yaşam döngüsü, projeksiyon sahipliği, nullable-unique, rezervasyon politikası, API sözleşmesi ve tek şube anahtarı dokümanları koda göre düzeltilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-715-table-doc-drift-and-boundary-fixes.md`
- `evidence/V1-RMD-715/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/** - yalnız Tables ile ilgili doküman düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/** - yalnız audit yazımı ve DI kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/** - yalnız DI kaydının taşınması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Tables/** - yalnız yeni ve düzeltilen test
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Doküman düzeltmeleri, audit yazımı, DI taşıma, mimari test.

## Out of scope

- Davranış değişikliği getiren masa düzeltmeleri (V1-RMD-710 ile V1-RMD-714 ve V1-RMD-716).
- Gönderilmiş hesap kaydı tablosunun (V1-RMD-714) doküman satırı bu görevde yazılır.
- Migration 205'in (V1-RMD-714) kısıtları `docs/data` altındaki nullable-unique ve tablo sahipliği dokümanlarına bu görevde işlenir.

## Dependencies

- V1-RMD-601

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-715/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-716

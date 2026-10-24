# V1-RMD-714 - Masa veri kısıtları, doküman ve test boşlukları

- Task ID: V1-RMD-714
- Status: Planned
- Assignee: unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulguları B023, B025, B027, B030, B031 (tekrar gönderim yanıtı), B032 ve B033 kapanır. Aktif birleşim ve aktif rezervasyon için kısmi benzersiz indeksler eklenir; eşzamanlılık, tekrar teslim ve kısmen ödenmiş birleşim testleri eklenir; yaşam döngüsü, projeksiyon, nullable-unique, rezervasyon politikası ve bağımlılık dokümanları koda göre düzeltilir; Tables'ın ham audit INSERT'leri `IAuditEventStore` üzerinden yazılır, modül DI kaydı modüle taşınır ve Payments okuması dokümana yazılır; ölü masa olayı için uyarı ve işaretçi onarım işi, kasaya gönderilmiş siparişin masadan ayrılması ve olay kimliği tekilleştirmesi ele alınır (V1-RMD-601 S2, S4, S5, S6, S7).

## Owned surface

- `plan/v1/remediation/V1-RMD-714-table-db-constraints-docs-and-tests.md`
- `evidence/V1-RMD-714/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/** - yalnız yeni migration
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/** - yalnız Tables ile ilgili doküman düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-601-tables-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kısmi benzersiz indeksler (up ve down), test boşlukları, doküman sapmaları, Düşük bag.

## Out of scope

- Mevcut migration dosyalarını değiştirmek.

## Dependencies

- V1-RMD-601

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-714/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-602

# V1-RMD-711 - Masa olayları, transfer ve durum ucu güvenliği

- Task ID: V1-RMD-711
- Status: Planned
- Assignee: unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulguları B005, B006, B007 (transfer ve durum ucu), B021, B022, B024 ve B025 kapanır. Birleşim, transfer ve durum olayları taşınacak sipariş ve hesap kimliklerini taşır ve tüketici yalnız onları taşır; tüketiciler olay kimliğiyle tekilleştirir; transfer ve genel durum ucu aktif birleşimi ve açık sipariş veya hesabı denetler ve denetim kaydı yazar; masa PUT ile bölge ve kat planı yazmaları denetim kaydı bırakır; status_changed_at her durum geçişinde güncellenir; 40001 ve 40P01 sınırlı yeniden denenir; işaretçi yeniden kurma zamanlı işle çalışır; ölü (dead) masa olayı için uyarı ve yeniden oynatma yolu bulunur (V1-RMD-601 S1, S2, S3).

## Owned surface

- `plan/v1/remediation/V1-RMD-711-table-event-and-transfer-safety.md`
- `evidence/V1-RMD-711/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/** - yalnız olay tüketicisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/** - yalnız olay tüketicisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-601-tables-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Olay yükleri, tüketici tekilleştirme, transfer ve durum ucu denetimi, denetim kaydı, status_changed_at, hata eşlemesi, işaretçi onarım işi.

## Out of scope

- Birleşim olaylarına kimlik listesi eklenmesi (V1-RMD-710 yaptı; bu görev tüketici tekilleştirmesini ve diğer olayları yapar).
- Rezervasyon alanları ve istemci düzeltmeleri (V1-RMD-712, V1-RMD-713).

## Dependencies

- V1-RMD-601

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-711/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-602

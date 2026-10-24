# V1-RMD-711 - Masa olayları, transfer ve durum ucu güvenliği

- Task ID: V1-RMD-711
- Status: Planned
- Assignee: unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulguları B005 (S1, S3), B006, B007 (transfer, durum ve masa düzenleme), B021, B022 ve B024 kapanır. Birleşim ve transfer olayları taşınacak sipariş ve hesap kimliklerini taşır; tüketiciler yalnız o kimlikleri ve yalnız hâlâ beklenen masada duranları taşır; eski bir birleşim olayı yeniden teslim edilse de ayrılmış birleşimi geri kurmaz; transfer aktif birleşimdeki masayı reddeder; genel durum ucu açık siparişi veya hesabı olan ya da aktif birleşimdeki masayı boşaltmayı reddeder ve her durum değişikliği denetim kaydı bırakır; masa düzenleme dolu veya rezerveli masayı pasife almayı reddeder; bölge, masa ve kat planı yazmaları denetim kaydı bırakır; status_changed_at her durum geçişinde güncellenir; 40001 ve 40P01 çakışma olarak (409) döner.

## Owned surface

- `plan/v1/remediation/V1-RMD-711-table-event-and-transfer-safety.md`
- `evidence/V1-RMD-711/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/** - yalnız olay tüketicisi ve taşıma sorgusu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/** - yalnız olay tüketicisi ve taşıma sorgusu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/IntegrationContracts/TableIntegrationEvents.cs - yalnız olay yüklerine kimlik listeleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderSettlementService.cs - yalnız status_changed_at
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/CashierHandoffStore.cs - yalnız status_changed_at
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs - yalnız Tables ana makine kenarına Audit
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md - yalnız Tables ana makine kenarı satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-601-tables-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Olay yükleri ve tüketici sorguları, birleşim etkinlik koruması, transfer ve durum ucu denetimi, masa düzenleme koruması, denetim kaydı, status_changed_at, hata eşlemesi.

## Out of scope

- Olay kimliğiyle tekilleştirme için yeni bir inbox tablosu, ölü olay uyarısı ve işaretçi onarım işi, B025 (V1-RMD-714), B030 (V1-RMD-714).
- Rezervasyon alanları ve istemci düzeltmeleri (V1-RMD-712, V1-RMD-713).

## Dependencies

- V1-RMD-601
- V1-RMD-710

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-711/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-602

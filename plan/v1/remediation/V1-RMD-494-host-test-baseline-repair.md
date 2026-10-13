# V1-RMD-494 - Host test tabanı onarımı (anonimleştirme kaydı ve QR test şeması)

- Task ID: V1-RMD-494
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Temiz commit'te de kırmızı olan iki Host testini kök nedenleriyle onarmak: serve kapsayıcısı KVKK anonimleştirme alanlarını (`IAnonymizationPlans`) çözemiyor, ve QR onay testinin şeması modifier tüketim kolonunu (migration 174) içermiyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-494-host-test-baseline-repair.md`
- `evidence/V1-RMD-494/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs — yalnız anonimleştirme alanları hizmetinin serve kapsayıcısına kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/Confirmation/ALKAROS.Host.Experience.Orders.Confirmation.Tests.csproj — yalnız 174 numaralı migration'ın test şemasına eklenmesi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `ProductionExperienceCompositionTests.ServeContainerResolvesEveryModuleServiceFromTheModuleCatalog` ve `QrConfirmationReservationHttpTests.AcceptingAQrOrderLocksEveryStockRowInTheGlobalOrderBeforeTheHoldTakesAny` testlerinin yeşile dönmesi; ürün davranışı değişmez.

## Out of scope

- Anonimleştirme kurallarının, KVKK akışının veya QR onay mantığının değiştirilmesi.

## Dependencies

- V15-KVK-002
- V1-RMD-486

## Acceptance evidence

- Önce-sonra test çıktıları ve ilgili Host test projelerinin tam koşusu; çıktılar `evidence/V1-RMD-494/` altındadır.

## Handoff

- None

# V1-RMD-659 - Host orkestrasyon kenar listesinin kapsamı

- Task ID: V1-RMD-659
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-595 bulgusu F25'in kalan parçası: `HostOrchestrationEdgesStayWithinTheApprovedList` yalnız Orders, KitchenOperations ve Recipes ağaçlarını denetliyordu; Billing, DualScreen, CustomerAccounts, Observability ve SecurityAdministration Host ağaçlarındaki modül kenarları hiçbir şey tarafından denetlenmiyordu. Bu ağaçlar listeye girer (mevcut gerçek kenarların birleşimi, yeni kenar sessizce eklenemez) ve belge tablosu aynı değişiklikte güncellenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-659-host-orchestration-edge-coverage.md`
- `evidence/V1-RMD-659/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-658-audit-docs-small-drifts.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs - yalnız ApprovedHostOrchestrationEdges girdileri
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md - yalnız Host/Experience orchestration edges tablosu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Beş yeni Host ağacı için onaylı kenar girdileri ve belge satırları; üretim kodu değişmez.

## Out of scope

- Kenarları daraltmak (mevcut gerçek kenarlar olduğu gibi kayda geçer).

## Dependencies

- V1-RMD-658

## Acceptance evidence

1. Yeni girdilerle `HostOrchestrationEdgesStayWithinTheApprovedList` ve diğer mimari paketler yeşil; her girdi için tek kenar çıkarılınca (mutasyon) test kırılır.
2. Çıktılar `evidence/V1-RMD-659/` altındadır.

## Handoff

- None

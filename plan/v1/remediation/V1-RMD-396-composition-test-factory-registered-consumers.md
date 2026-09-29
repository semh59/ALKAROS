# V1-RMD-396 - Composition testi factory ile kaydedilmiş çoklu servisleri de doğrulasın

- Task ID: V1-RMD-396
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V12-TGO-003, `TrendyolGoStatusUpdateConsumer`'ı factory ile kaydetti (`ImplementationType` = servis türü).
`ServeContainerResolvesEveryModuleServiceFromTheModuleCatalog`'un çoklu kayıt dalı her descriptor için
`GetType() == ImplementationType` beklediğinden o commit'ten beri kırmızı (V1-RMD-393 F-03). Tekil kayıt dalı
factory'leri zaten ayrı ele alıyor. Çoklu dal, factory'yi serve container üzerinde çalıştırıp ürettiği somut
türün `GetServices` sonucunda bulunduğunu doğrular — yani factory kaydını atlamaz, gerçekten sınar.

## Owned surface

- `plan/v1/remediation/V1-RMD-396-composition-test-factory-registered-consumers.md`
- Sınırlı ek (V1-RMD-020 sahipliğinde kalır):
  tests/Host/Experience/Composition/ProductionExperienceCompositionTests.cs

## Dependencies

- V1-RMD-394

## Acceptance evidence

- `dotnet test tests/Host/Experience/Composition` exit 0 (bugün 1 test kırmızı).
- Mutasyon: `OnlineOrderingModule` içindeki Trendyol consumer kaydı kaldırıldığında test kırmızıya döner.

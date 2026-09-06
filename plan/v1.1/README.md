# V1.1 - Menu, Recipe, Production and Inventory

## Hedef

Günlük menü, immutable reçete sürümü, üretim ve ortak porsiyon stok havuzunu
kurmak.

## Giriş koşulu

`GATE-V11-ENTRY` kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 26 görev dosyasının tamamı `Done` (24 özellik görevi +
  2 remediation: `V11-RMD-001` migration ID çakışması ve canlı kompozisyon
  kablolaması, `V11-RMD-002` 5 modülün mimariye kaydı ve Inventory
  şema sınırı düzeltmesi, 2026-09-06).
- Unit conversion boyut güvenliğiyle çalışır.
- ProductionBatch geçmiş RecipeVersion'ı değiştiremez.
- Stok hareket defteri ile balance projection yeniden üretilebilir.
- Son porsiyon yarışı ve reservation lifecycle eşzamanlılık testlerinden geçer.
- **`GATE-V11-EXIT` kesin olarak mühürlendi (`V11-GOV-003`, 2026-09-06).**
  Migration çakışması kapandı, V1.1'in 17 migrasyonu gerçek kompozisyona
  bağlı (`V11-RMD-001`); `Inventory`, `Recipes`, `Production`, `Purchasing`,
  `Menu` artık `IModule` uyguluyor ve `ModuleRegistry.DefaultCatalog`/
  `ALKAROS.Host.csproj`/`ModuleBoundaryTests`'e kayıtlı; doğrulanan
  cross-schema yazma ihlalleri (Production/Purchasing → `inventory.*` ham
  SQL) Inventory'nin kendi contract'ı üzerinden düzeltildi (`V11-RMD-002`).
  `master`'a birleştirme kararı ayrı, Semih'i bekliyor.

## Modüller

`daily-menu`, `inventory`, `operations-ui`, `portion-reservation`, `production`,
`purchasing`, `reporting`, `units-recipes`, `remediation`.

Doğrulanan plan hacmi: 9 modül, 26 tek-sahip görev.

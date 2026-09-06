# V1.1 - Menu, Recipe, Production and Inventory

## Hedef

Günlük menü, immutable reçete sürümü, üretim ve ortak porsiyon stok havuzunu
kurmak.

## Giriş koşulu

`GATE-V11-ENTRY` kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 25 görev dosyasının tamamı `Done` (24 özellik görevi +
  1 remediation: `V11-RMD-001`, migration ID çakışması ve canlı kompozisyon
  kablolaması, 2026-09-06).
- Unit conversion boyut güvenliğiyle çalışır.
- ProductionBatch geçmiş RecipeVersion'ı değiştiremez.
- Stok hareket defteri ile balance projection yeniden üretilebilir.
- Son porsiyon yarışı ve reservation lifecycle eşzamanlılık testlerinden geçer.
- **`GATE-V11-EXIT` henüz mühürlenmedi.** Migration çakışması kapandı ve
  V1.1'in 17 migrasyonu artık gerçek kompozisyona bağlı (`V11-RMD-001`),
  ama 5 modülün (`Inventory`, `Recipes`, `Production`, `Purchasing`, `Menu`)
  `IModule`/`ModuleRegistry`/Host'a hiç kayıtlı olmaması ve doğrulanmamış
  cross-schema yazma iddiaları (`docs/audit/INDEPENDENT_DEEP_AUDIT_2026-09-06.md`)
  açık; `master`'a birleştirme kararı bekliyor.

## Modüller

`daily-menu`, `inventory`, `operations-ui`, `portion-reservation`, `production`,
`purchasing`, `reporting`, `units-recipes`, `remediation`.

Doğrulanan plan hacmi: 9 modül, 25 tek-sahip görev.

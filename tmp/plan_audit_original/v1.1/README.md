# V1.1 - Menu, Recipe, Production and Inventory

## Hedef

Günlük menü, immutable reçete sürümü, üretim ve ortak porsiyon stok havuzunu
kurmak.

## Giriş koşulu

V1 çıkış kapısı kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 23 görev dosyasının tamamı `Done`.
- Unit conversion boyut güvenliğiyle çalışır.
- ProductionBatch geçmiş RecipeVersion'ı değiştiremez.
- Stok hareket defteri ile balance projection yeniden üretilebilir.
- Son porsiyon yarışı ve reservation lifecycle eşzamanlılık testlerinden geçer.

## Modüller

`daily-menu`, `inventory`, `operations-ui`, `portion-reservation`, `production`,
`purchasing`, `reporting`, `units-recipes`.

Doğrulanan plan hacmi: 8 modül, 23 tek-sahip görev.

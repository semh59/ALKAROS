# V1-RMD-233 - remainingCount: çoklu stok eşlemesi ve eksik balance test kapsamı

- Task ID: V1-RMD-233
- Status: Done
- Assignee: claude-code-session_01Xsqh6z1RYhmFapKkHKoBmk
- Work type: implementation
- Surface state: Existing

## Goal

`DualScreenStore.GetCatalogAsync`'in `remainingCount` hesaplaması
(`MIN(FLOOR(sb.available_quantity / psm.quantity_multiplier))`, bir
ürünün birden fazla `product_stock_mappings` satırı olduğunda en kısıtlayıcı
stok kalemini seçen `LEFT JOIN`), bağımsız bir denetim ajanı tarafından
(2026-09-17, Kasa modülü kapsamlı denetimi) şu açıdan test edilmemiş
bulundu:

1. Aynı ürüne bağlı BİRDEN FAZLA stok kalemi olduğunda (biri diğerinden
   daha kısıtlayıcı) doğru minimum'un seçildiği hiç test edilmemiş.
2. Bir ürünün mapping'i var ama karşılık gelen `stock_balances` satırı
   YOKSA, `LEFT JOIN` bu satırı `MIN()` içinde sessizce yok sayıyor —
   sonuç `RemainingCount = null` (yani "sınırsız"). Bu, stok kaydı hiç
   girilmemiş bir ürünün müşteri ekranında/garson-kasa kataloğunda
   SINIRSIZ gösterilmesi anlamına geliyor; bu davranışın KASITLI mı
   yanlışlıkla mı olduğu koddan belli değil ve hiçbir test bunu
   doğrulamıyor.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs
  (V1-WTR-054 sahipliğinde kalır) — yalnız yeni test senaryoları eklenir;
  mevcut testler değiştirilmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenStore.cs
  (V1-WTR-054 sahipliğinde kalır) — YALNIZ eğer "balance kaydı yoksa
  sınırsız göster" davranışının yanlış olduğuna karar verilirse (bkz.
  Onay/karar notu gereksinimi aşağıda), sorguya bir düzeltme eklenir;
  karar "mevcut davranış doğru" ise bu dosyaya dokunulmaz.
- `evidence/V1-RMD-233/**`

## In scope

1. Test: bir ürüne bağlı iki farklı `product_stock_mappings` satırı (biri
   diğerinden daha kısıtlayıcı miktar üretecek şekilde) olduğunda, doğru
   (en düşük) `remainingCount`'un döndüğünü kanıtlamak.
2. Test: bir ürünün TEK mapping'i var ama `stock_balances` satırı yoksa,
   `remainingCount`'un ne döndüğünü (şu an: `null`) açıkça belgeleyen ve
   bunun kasıtlı bir davranış olduğunu doğrulayan bir test — VEYA bu
   davranışın yanlış olduğuna karar verilirse (stoksuz/kayıtsız ürün
   "sınırsız" değil "0" göstermeli), düzeltme + test.
3. Bu kararı (sınırsız mı, sıfır mı) net bir cümleyle Acceptance
   evidence'a yazmak — belirsiz bırakılmaz.

## Out of scope

- `remainingCount` hesaplamasının SQL formülünün kendisi (`FLOOR`,
  çarpan mantığı) — zaten doğru, bu görev yalnız eksik test kapsamını ve
  belirsiz bir edge-case davranışını kapatıyor.

## Dependencies

- V1-WTR-054

## Acceptance evidence

- İki yeni test (çoklu mapping, balance-yok senaryosu) eklenir ve geçer.
- `DualScreenStoreTests` (tamamı, gerçek Postgres'e karşı) → regresyonsuz
  geçer.
- "Balance kaydı yoksa sınırsız/sıfır" davranışı için AÇIK bir karar cümlesi
  ve gerekçesi.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None

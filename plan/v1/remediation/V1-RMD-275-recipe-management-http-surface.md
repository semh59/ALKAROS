# V1-RMD-275 - Reçete kataloğu ve özel birim dönüşümleri yönetici uç noktalarından yönetilebilir

- Task ID: V1-RMD-275
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`V1-RMD-273` borç listesinin "reçete sürümleme ve birim dönüşümü" ailesi. Çalışan uygulamada
reçete OLUŞTURACAK hiçbir yol yoktu: `V11-RCP-003` ürün-reçete eşleme yüzeyi ve `V1-RMD-247` maliyet
anlık görüntüsü yüzeyi, var olmayan reçetelere işaret ediyordu. `V11-RCP-001` `RecipeLifecycleService`
(değişmez sürümlü reçete) ve `V11-UNT-001` birim dönüşüm deposu yazılmış, DI'da kayıtlı, çağıransızdı.

Mevcut yönetici reçete grubuna (`inventory.manage`) eklendi: reçete listele/oluştur, sürüm listele,
ilk taslak / sonraki taslak, taslağa malzeme ekle/çıkar, sürümü etkinleştir, kilitle; özel birim
dönüşümü listele/ekle (`kasa → adet`). Etkinleştirilmiş ya da kilitli sürüm DEĞİŞTİRİLEMEZ
(`409 VERSION_IMMUTABLE`; değişiklik yeni taslak sürümle yapılır): maliyet anlık görüntüleri ve tüketim
kayıtları sürümün altında değişmez. Aynı kodlu reçete `409`; birim dönüşümü çift başına tektir, tekrar
gönderim katsayıyı değiştirir.

## Owned surface

- `plan/v1/remediation/V1-RMD-275-recipe-management-http-surface.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Recipes/RecipeManagementEndpoints.cs
  (V11-RCP-003 sahipliğindeki klasöre eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Recipes/RecipeCatalogMappingEndpoints.cs
  (V11-RCP-003 sahipliğinde kalır — yalnız servis kayıtları, `MapRecipeManagement()` çağrısı ve hata eşlemeleri)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Recipes/RecipeManagementHttpTests.cs
  (aynı test projesine eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Recipes/RecipeCatalogMappingTestDatabase.cs
  (yalnız stok kalemi tohumlama yardımcısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json
  (V1-RMD-272 sahipliğinde — yalnız ulaşılabilir olan reçete/birim girişleri silindi)

## In scope

1. Reçete ve sürüm yaşam döngüsü uç noktaları, birim dönüşümü uç noktaları, hata eşlemesi.
2. Gerçek Postgres HTTP testleri, borç listesinden çıkarma.

## Out of scope

- Reçete yönetim ekranı (yalnız HTTP yüzeyi).
- Reçete silme/arşivleme (servis sunmuyor).
- Malzeme güncelleme (yalnız ekle/çıkar).

## Dependencies

- V11-RCP-001
- V11-UNT-001
- V11-RCP-003
- V1-RMD-272

## Acceptance evidence

- Host.Experience.Recipes (UTF8 Postgres 18): 14/14. Yeni: anonim 401 / yetkisiz 403; reçete oluştur → çift kod 409 →
  taslak (Draft, sürüm 1) → malzeme ekle (500 g, %5 fire) → etkinleştir (Active) → etkin sürüme malzeme eklemek
  409 `VERSION_IMMUTABLE` → sonraki taslak sürüm 2; bilinmeyen reçete 404, sıfır verim 400; birim dönüşümü
  ekle/listele/tekrarda katsayı değişir.
- Modül sınır testleri 9/9; `consistency_audit.py` ve `plan_audit_tool.py validate` temiz.

## Handoff

- None

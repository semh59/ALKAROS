# V1-RMD-245 - Refresh stale hardcoded module-count and manifest-id test literals

- Task ID: V1-RMD-245
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`tests/Host/MigrationComposition` içindeki iki test dosyası, tam
`dotnet test` koşusu bu görev serisinin bir parçası olarak çalıştırılırken
bağımsız olarak bulundu (bu görevin kendi değişikliklerinden değil,
önceki oturumlardan kalan bir drift'ten kaynaklanıyordu — `git status`
ile doğrulandı, bu dosyalar bu oturumda daha önce hiç değiştirilmemişti):

- `HostModuleReachabilityTests.DefaultCatalogContainsStandardProductionModules`
  `ModuleRegistry.DefaultCatalog`'un tam olarak 19 modül içerdiğini
  varsayıyordu; gerçek sayı (Invoicing/Token/QNB/CashSession modüllerinin
  önceki oturumlarda eklenmesiyle) 24'e çıkmış.
- `ManifestTests.RuntimeManifestContainsOnlyImplementedMigrationPairs`
  gerçek `database/MigrationComposition/order.json`'ı yükleyip (fixture
  bir kopya değil, doğrudan gerçek dosyaya bağlı) sabit kodlanmış bir
  ID listesi/sayı/son tablo adıyla karşılaştırıyordu; bu literal kendi
  yorumunun da itiraf ettiği üzere (098/099'da) daha önce iki kez bayatlamış,
  bu sefer 126-131 arası (bu oturumun V1-RMD-236/241/242 migration'ları)
  eklenene kadar güncellenmemişti.
- `CustomerDisplayContractTests.CatalogProductCarriesRealCategoryMetadata`
  aynı "asserted exactly allowlist" deseniyle, `CatalogProductDto`'ya
  önceki bir oturumda (V1-CUI-010, remainingCount/last-portion rozeti)
  eklenen gerçek bir alanı hiç yansıtmıyordu — gerçek `dotnet test` koşusu
  sırasında bulundu, aynı diffte düzeltildi.

## Owned surface

- `evidence/V1-RMD-245/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs,
  tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs
  (V1-FND-008 sahipliğinde kalır) — yalnız bayat sabit sayılar/listeler
  gerçek duruma güncellendi; test mantığının kendisi değişmedi.

## In scope

- `19` → `24` (gerçek modül sayısı).
- `RuntimeManifestIds`/`LastEntryTables`/`124` → gerçek `order.json`'ın
  şu anki tam hâli (131'e kadar).

## Out of scope

- Bu iki testin altta yatan "gerçek dosyaya karşı sabit kodlanmış literal"
  tasarımını (tekrar bayatlayacağı garanti) yeniden tasarlamak — kapsamı
  aşan ayrı bir mimari karar, bu görev yalnız mevcut drift'i düzeltir.

## Dependencies

- None

## Acceptance evidence

- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj`
  → ilgili iki test artık yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None

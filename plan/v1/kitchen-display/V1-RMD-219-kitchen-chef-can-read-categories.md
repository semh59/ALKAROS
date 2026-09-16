# V1-RMD-219 - kitchen-chef, rota formunun kategori listesini artık görebiliyor

- Task ID: V1-RMD-219
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) bulduğu **HIGH** bulgu:
Mutfak ekranının rota formu, kategori listesini Catalog'un kendi
`alkaros.manager` çerezine bağlı, `catalog.manage` yetkisi isteyen
uç noktasından (`/api/v1/management/catalog/categories`) çekiyordu.
`kitchen-chef` rolü `kitchen.routing.manage` taşıyor ama bilinçli
olarak `catalog.manage` almıyor (V1-IAM-029) — bu yüzden istek her
zaman 401 dönüyordu, `kitchenApi.ts`'in `if (!response.ok) return
[];` satırı bunu sessizce yutuyordu, ve dropdown bu rol için hep boş
kalıyordu; backend'in kendisine tam yetki verdiği "kategori rotası
oluştur" özelliğini arayüzden asla kullanamıyordu.

## Owned surface

- src/Host/Experience/KitchenOperations/KitchenOperationsContracts.cs
  (ilgili modülün sahipliğinde) — yeni `KitchenCategoryV1`.
- src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs
  (aynı modül) — yeni `GetCategoriesAsync`.
- src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs
  (aynı modül) — yeni `GET /categories`.
- src/Clients/PosTerminal/src/features/kitchen-operations/kitchenApi.ts
  (ilgili modül) — `getCategories()` artık bu yeni terminal-scoped uca
  gidiyor.
- tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs,
  src/Clients/PosTerminal/src/features/kitchen-operations/kitchenApi.test.ts
  (aynı modüller)

## In scope

1. Yeni `GET /kitchen-operations/categories` — `catalog.categories`'i
   doğrudan (aynı modülün `/printers`/`/routes` uçlarıyla birebir aynı
   `RequireReadAsync` yetki seviyesi) okuyor. Bir kategori adı,
   `catalog.manage`'in koruduğu bir yönetim kararı değil — yalnız
   oluşturma/düzenleme öyle.
2. Frontend artık Catalog'un yönetim ucuna hiç gitmiyor; kendi
   terminal-scoped `/categories`'ini çağırıyor.
3. Yeni testler: gerçek Postgres'e karşı, hiçbir izin taşımayan bir
   oturumun (`/printers`/`/routes` ile aynı seviye) kategorileri
   okuyabildiğini kanıtlıyor; frontend tarafında da artık eski yönetim
   ucuna hiç gidilmediğini doğruluyor.

## Out of scope

- `kitchen-chef`'e `catalog.manage` vermek — bu, V1-IAM-029'un
  bilinçli kararını (fiyat/menü değişikliği yetkisi vermeme) bozar;
  doğru çözüm daha az ayrıcalıklı bir okuma ucu, yetki genişletmek
  değil.

## Dependencies

- V1-IAM-029

## Acceptance evidence

- `npx tsc --noEmit` (PosTerminal) → 0 hata.
- `npx vitest run` (PosTerminal, tüm proje) → 172/172 yeşil, yeni test
  dahil.
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı `tests/Host/Experience/KitchenOperations` →
  34/34 yeşil; revert-and-confirm ile hem backend hem frontend testi
  ayrı ayrı gerçekten kırılıp doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None

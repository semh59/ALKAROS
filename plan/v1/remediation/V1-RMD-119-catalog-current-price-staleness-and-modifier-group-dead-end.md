# V1-RMD-119 - Deep audit wave 3: catalog current_price staleness and modifier-group dead-end

- Task ID: V1-RMD-119
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Düzeltme planının Dalga 2'si (`deep-catalog.md`'nin 2 Critical bulgusu):
`catalog.products.current_price` — gerçek satışın tek okuduğu sütun —
yalnızca `POST /prices`'ın yan etkisi olarak, hem de yalnız yeni satır o
anda zaten geçerliyse güncelleniyordu; hiçbir arka plan işi onu asla tekrar
gözden geçirmiyordu. Ayrıca `POST /modifier-groups` hiçbir istemciden hiç
çağrılmıyordu — bir operatör tekil modifikatör oluşturabiliyor ama zorunlu
bağlı olduğu grubu asla oluşturamıyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-119-catalog-current-price-staleness-and-modifier-group-dead-end.md` (yeni)
- Sınırlı ek — aşağıdaki yollar (yeni dosya dahil) ilgili görevlerin
  sahipliğinde kalır (yollar geri-tik olmadan yazıldı ki denetleyici
  bunları sahiplik iddiası olarak parse etmesin):
  - src/Host/Experience/Catalog/CatalogManagementStore.cs, CatalogManagementEndpoints.cs,
    CatalogPriceRecomputeHostedService.cs (yeni dosya) (V1-RMD-076
    sahipliğinde) — CreatePriceAsync'in koşulsuz recompute'a geçmesi, yeni
    arka plan işi, hosted service kaydı.
  - src/Clients/PosTerminal/src/features/catalog/{models,catalogApi,CatalogWorkspace}.tsx|.ts,
    src/Clients/PosTerminal/src/strings.ts (V1-CUI-004 sahipliğinde) —
    modifierGroups create yüzeyi.
  - tests/Host/Experience/Catalog/CatalogManagementHttpTests.cs (V1-RMD-014
    sahipliğinde), src/Clients/PosTerminal/src/features/catalog/{catalogApi,CatalogWorkspace}.test.tsx|.ts
    (V1-CUI-004 sahipliğinde) — bu görevin bulgularına karşılık gelen
    regresyon testleri.

## In scope

1. **`current_price` önbellek bayatlaması (Critical, Catalog C-1).**
   `CatalogManagementStore.CreatePriceAsync` artık her `SalePrice` eklemesinden
   sonra koşulsuz olarak `IPricingRepository.GetEffectivePriceAsync(now)`'ı
   çağırıp `current_price`'ı gerçek geçerli fiyata (veya hiçbiri yoksa
   `null`'a) eşitliyor — eski kod yalnız YENİ eklenen satırın o an geçerli
   olup olmadığına bakıyordu, bu da bir ürünün eski (süresi dolmuş) fiyatı
   önbellekte kalmışken gelecek için planlanmış yeni bir fiyat eklendiğinde
   önbelleği HİÇ temizlemiyordu (regresyon testiyle doğrulandı). Yeni
   `CatalogPriceRecomputeHostedService` (60 saniyede bir çalışan arka plan
   işi) yazma-zamanlı düzeltmenin asla kapatamayacağı kalan durumu
   kapatıyor: hiçbir yazma olmadan zamanlanmış bir fiyatın etkinleşmesi
   veya bir promosyonun süresinin dolması. Tek para birimi (TRY) ve tek
   `price_type` (`SalePrice`, DB'nin kendi `CHECK (price_type IN (1))`
   kısıtıyla zaten dayatılıyor) varsayımı açıkça belgelendi — sistemin geri
   kalanının zaten yaptığı varsayımla tutarlı.
   **Doğrulama sırasında bulunup düzeltilen gerçek bir regresyon:**
   `RecomputeAllAsync`'in ilk sürümü, `product_prices`'ta hiç satırı olmayan
   bir ürünü (`CreateProductV1.CurrentPrice` ile doğrudan tohumlanmış,
   tarihli fiyatlandırma sistemine hiç girmemiş bir ürün — deep-catalog'un
   kendi H-3 bulgusunun ele aldığı senaryo) süresi dolmuş bir fiyatla aynı
   kefeye koyup `current_price`'ını `null`'a çekiyordu; tam test paketi
   (`DualScreenAuthorizationHttpTests.CatalogHttpContractKeepsLegacyArrayAndProvidesFilteredStableBoundedContinuation`)
   bunu gerçek bir başarısızlıkla (4 beklenirken 0) yakaladı. Sorgu artık
   yalnız `product_prices`'ta en az bir satırı olan ama hiçbiri şu an geçerli
   olmayan ürünleri temizliyor; yeni
   `RecomputeAllAsyncNeverClearsASeedPriceForAProductWithNoPricingHistoryRow`
   testi bu ayrımı kalıcı olarak kilitliyor.
2. **Modifier-group oluşturma çıkmaz sokağı (Critical, Catalog C-2).**
   `CatalogEntityKind`/`CatalogCreateInput`/`catalogApi.ts`'in `create()`'i
   hiç `"modifierGroups"` durumu içermiyordu. PosTerminal'in katalog yönetim
   ekranına yeni bir sekme, form (kod/ad/seçim türü/min-max seçim) ve
   doğrulama eklendi; artık `POST /modifier-groups`'a gerçekten ulaşıyor —
   `POST /modifiers`'ın zaten çalışan aynı deseniyle.

## Out of scope

- **`product-modifier-assignments` GET/POST çifti** — deep-catalog raporunun
  aynı bulgu grubuna dahil edilmiş ayrı bir kalıntı (UI'ın kendi "modifikatör
  atamaları" paneli `Modifier.ProductId`'yi kullanıyor, bu join table'ı
  değil) — ayrı bir karar/dalga.
- **`GET /effective-price`'ın kendisi** — zaten canlı sorguluyor ve doğru
  çalışıyordu (bu, iki görünümün neden birbirinden ayrıştığının kanıtıydı);
  dokunulmadı.
- Deep-catalog raporunun geri kalan High/Medium/Low bulguları
  (availability-toggle'ın client-supplied version taşımaması; H-3 seed
  fiyatlı ürün oluşturmanın `product_prices` geçmiş satırı üretmemesi;
  PUT/PATCH/DELETE'in hiç var olmaması) — ayrı dalgalara bırakıldı.

## Dependencies

- V1-RMD-118

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml up --build test` +
  `docker wait` (gerçek konteyner çıkış kodu doğrulandı, `| tail` ile
  maskelenen sahte-yeşil sonuçtan kaçınmak için): tüm proje testleri yeşil
  (yeni testler dahil:
  `CatalogManagementHttpTests.CreatingAScheduledFuturePriceClearsAnAlreadyExpiredCurrentPrice`,
  `...RecomputeAllAsyncActivatesScheduledPricesAndClearsExpiredOnesWithNoAccompanyingWrite`,
  `...RecomputeAllAsyncNeverClearsASeedPriceForAProductWithNoPricingHistoryRow`).
  İlk koşuda `ALKAROS.Host.Tests.dll`'in gerçek bir testi (yukarıdaki
  regresyon) yakaladı; düzeltme sonrası ikinci koşu 121/121 ve
  `ALKAROS.Host.Experience.Catalog.Tests.dll` 10/10 yeşil.
- `npx tsc --noEmit` (PosTerminal): sıfır hata.
- `npx vitest run` (PosTerminal): 19 dosya, 123 test, hepsi geçti (yeni
  `posts a typed modifier group create to its own resource` ve
  `creates a modifier group — the create surface that was entirely
  unreachable before this fix` dahil).
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata.
- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi bu
  görevden önce de vardı, dokunulmayan dosyalarda.

## Handoff

- V1-GOV-115

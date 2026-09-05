# V1-RMD-105 - Low cleanup: dead OrderEntry engines, ItemException IsManagerAuthorized flag, dead Pricing Update/Delete

- Task ID: V1-RMD-105
- Status: Done
- Assignee: claude-session-01Dhks7X2RG1fxScJpZRzZiL
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("Hepsini çöz", 2026-09-05), eski denetim + 2026-09-05
4-ajan denetiminin geri kalan Low / kod-kalite maddeleri temizlendi:

1. **OrderEntry ölü C# istemci modülleri** — `V1-RMD-104`'te kaldırılan
   `SessionQueue` ile aynı sınıf. `OrderEntryEngine.BeginSubmission()`
   (çift-tıklama koruma motoru, `V1-RMD-102` Bulgu 4'te not edildi) hiçbir
   yerden referans edilmiyordu; gerçek JS istemcileri (`cashier-app.js`,
   `waiter-app.js`) `V1-RMD-102`'de kendi korumasını aldı.
2. **`ItemExceptionHandler.IsManagerAuthorized`** — 2026-09-05 denetiminde
   not edilen yanıltıcı, işlevsiz defense-in-depth katmanı: iki HTTP
   çağıranı da bunu sabit `true` geçiyordu ve `UnauthorizedItemOperationException`
   istisna filtresinde eşlenmemişti (ileride `false` geçen bir çağıran
   temiz 403 yerine ham 500 alırdı). Yetkilendirme zaten HTTP sınırında
   (grant akışı) uygulanıyor.
3. **`IPricingRepository.UpdateAsync`/`DeleteAsync`** — eski denetimin B6
   bulgusunun `catalog.product_prices` kısmı: bu tabloda `row_version` yoktu
   ama `UpdateAsync`/`DeleteAsync`'in kendisi de ölüydü (hiç çağrılmıyordu;
   fiyat değişikliği yeni tarihli bir satırdır — append). `catalog.products`
   için `row_version` `V1-RMD-103`'te zaten eklendi (orada gerçek canlı
   risk vardı).

## Owned surface

- `plan/v1/remediation/V1-RMD-105-low-dead-code-and-misleading-layer-cleanup.md`
- Sınırlı ek — hiçbir yeni yüzey sahiplenilmedi; aşağıdaki tüm yollar
  ilgili görevin sahipliğinde kalır, bu dalgada yalnız dosya/metod silme
  yapıldı (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Clients/Cashier/OrderEntry ağacı ve tests/Clients/Cashier/OrderEntry
    ağacı (V1-CUI-002 sahipliğinde) — tamamen kaldırıldı (2 kaynak + test
    projesi, 4 test).
  - src/Clients/WaiterPwa/OrderEntry ağacı ve tests/Clients/WaiterPwa/OrderEntry
    ağacı (V1-WTR-002 sahipliğinde) — tamamen kaldırıldı (2 kaynak + test
    projesi, 4 test).
  - src/Modules/Orders/ItemExceptions ağacı ve tests/Modules/Orders/ItemExceptions
    ağacı (V1-ORD-004 sahipliğinde) — ItemExceptionCommands.cs'ten
    IsManagerAuthorized parametresi, ItemExceptionHandler.cs'ten iki ölü
    guard, ItemExceptionExceptions.cs'ten UnauthorizedItemOperationException
    tipi ve testten iki ölü test
    (VoidItemAsyncFailsClosedWhenNotManagerAuthorized,
    ApplyComplimentaryAsyncFailsClosedWhenNotManagerAuthorized) kaldırıldı;
    kalan çağrı yerlerinden IsManagerAuthorized: true argümanı çıkarıldı.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-IAM-024
    sahipliğinde) — new VoidOrderItemCommand / new ApplyComplimentaryCommand
    çağrılarından IsManagerAuthorized: true çıkarıldı; başka değişiklik yok.
  - src/Modules/Catalog/Pricing ağacı ve tests/Modules/Catalog/Pricing ağacı
    (V1-CAT-002 sahipliğinde) — IPricingRepository / PostgresPricingRepository'den
    UpdateAsync / DeleteAsync ve testten üç test (UpdatePersistsPriceAndBounds,
    UpdateOfMissingRowThrowsInvalidOperationException, DeleteRemovesPriceRecord)
    kaldırıldı; interface doc-comment "Read/write" → "Read and append".
  - ALKAROS.slnx (V1-RMD-036 sahipliğinde) — kaldırılan iki OrderEntry test
    projesinin girişi silindi.

## In scope

- 4 OrderEntry kaynak dosyası + 2 dedicated test projesi (8 test) silindi;
  `git grep` ile 12 public tipin hiçbirinin kendi dizinleri dışında
  referansı olmadığı doğrulandı.
- `IsManagerAuthorized` bayrağı/guard'ı/istisna tipi/iki testi kaldırıldı;
  `git grep "UnauthorizedItemOperationException\|IsManagerAuthorized"`
  yalnız açıklayıcı doc-comment'lerde kalıyor.
- `IPricingRepository.UpdateAsync`/`DeleteAsync` + implementasyonları + 3
  testi kaldırıldı; interface'in başka hiçbir implementasyonu/çağıranı yok
  (yalnız DI kaydı ve `AddAsync`/`GetEffectivePriceAsync` kullanan
  `CatalogManagementStore`).

## Out of scope

- `catalog.product_prices`'a `row_version` eklemek — mutasyon yolu artık
  yok, gereksiz.

## Dependencies

- V1-RMD-104

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` ve `-c Debug`: 0 uyarı / 0 hata
  (kaldırılan 2 test projesi olmadan).
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`, port 55432):
  `ALKAROS.Orders.ItemExceptions.Tests` 20/20 (22→20, iki ölü test
  kaldırıldı), `ALKAROS.Catalog.Pricing.Tests` 24/24 (27→24, üç ölü test
  kaldırıldı), `ALKAROS.Host.Experience.Orders.Comp.Tests` 7/7,
  `.Void.Tests` 5/5, `.VoidSent.Tests` 8/8,
  `ALKAROS.Host.Experience.Composition.Tests` 5/5,
  `ALKAROS.Orders.OrderAggregate.Tests` 102/102 — hepsi regresyonsuz. Tam
  çözüm koşusunda tek başarısız süit `ALKAROS.Host.Tests` (MigrationComposition,
  bu makinede `psql.exe` DLL boşluğu, G2 — bu dalgadan bağımsız).
- `python tools/consistency-audit/consistency_audit.py`: temiz.
- `python tools/plan-audit/plan_audit_tool.py validate` ve
  `validate-coverage`: sıfır hata.

## Handoff

- V1-GOV-088

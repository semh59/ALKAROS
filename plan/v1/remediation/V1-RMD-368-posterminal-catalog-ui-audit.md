# V1-RMD-368 - PosTerminal Katalog modül denetimi: yanlış hata sınıfı kontrolü

- Task ID: V1-RMD-368
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 8. modülü:
PosTerminal'in Katalog ekranı (`src/Clients/PosTerminal/src/features/catalog/**`, ~562 satır) ve
onu bağlayan `routes/workspace.tsx`'in `CatalogRoute` parçası. On iki boyut üzerinden tarandı.

Modül 7'de (billing) bulunan AYNI sistemik desen burada da vardı: `catalogApi.ts` kendi
`CatalogApiError` sınıfını fırlatıyor, ama dört çağrı noktası (`workspace.tsx`'in
`CatalogRoute.load()`'u, `CatalogWorkspace.tsx`'in ürün/kategori/vergi/modifikatör oluşturma,
kullanılabilirlik değiştirme ve hazırlama süresi güncelleme akışları) yalnızca paylaşılan
`ApiError`'ı kontrol ediyordu. `BillSplitWorkspace.test.tsx`'teki gibi, mevcut V1-RMD-114
regresyon testi de `new ApiError(...)` ile taklit ediyordu — üretimin gerçekte fırlattığı sınıfı
hiç sınamamış, bu yüzden boşluk hiç kırmızı olmamıştı.

Bu keşif sırasında AYNI desenin `routes/workspace.tsx`'in `TableRoute` (Modül 6, zaten
"Tamamlandı" işaretlenmiş) parçasında da var olduğu görüldü — `V1-RMD-367`'de ayrı olarak
düzeltildi (bkz o görev). Diğer tüm feature modüllerinin kendi workspace bileşenleri
(`authorization-decisions`, `kitchen-operations`, `online-*`, `pending-checks`) zaten kendi özel
hata sınıflarını doğru kontrol ediyor — yalnızca `billing` ve `catalog`'un `workspace.tsx`
rota-seviyesi `load()`'ları VE `CatalogWorkspace.tsx`'in kendi iç eylemleri bu boşluğa sahipti.
`kitchen-operations`/`system-health`/"authorization" rotalarının `workspace.tsx`'teki
`load()`'larında da AYNI desenin bulunduğu görüldü (ön inceleme) — bunlar sırasıyla Modül 9 ve
Modül 12'nin (ve modül listesinde açıkça yer almayan "authorization" alanının) kapsamında,
oraya varıldığında ayrıca ele alınacak.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-368-posterminal-catalog-ui-audit.md`

## In scope

1. **[T3/T6, Yüksek — hata yönetimi/frontend-backend uyumu] Dört çağrı noktası yalnızca
   `reason instanceof ApiError` kontrol ediyordu**: `workspace.tsx`'in `CatalogRoute.load()`'u,
   `CatalogWorkspace.tsx`'in kayıt oluşturma (`create`), kullanılabilirlik değiştirme
   (`toggleAvailability`) ve hazırlama süresi güncelleme (`savePrepTime`) akışları. Hepsi
   `reason instanceof ApiError || reason instanceof CatalogApiError` şeklinde düzeltildi.

## Out of scope

- Bu modülde ek bir ürün-katmanı (P1-P4) gözlemi bulunmadı; "86" terimi (menüden kaldırma)
  sektör jargonuna sadık ve zaten Türkçe açıklamayla eşleştirilmiş.
- `kitchen-operations`/`system-health`/authorization rotalarındaki aynı desenin `workspace.tsx`
  parçaları bilinçli olarak ertelendi — bkz Goal.

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (37 dosya,
  272 test, bu görevin 2 yeni testi dahil): 272/272 geçti, regresyon yok.
- Mutation-check: `CatalogWorkspace.tsx` + `workspace.tsx` `git stash` ile geri alındı, yeni iki
  test de GERÇEKTEN kırmızı oldu (jenerik yedek metinler gösterildi). `git stash pop` ile geri
  yüklendi, paket tekrar 272/272 yeşile döndü.

## Handoff

- None

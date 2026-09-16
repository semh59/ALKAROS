# V1-RMD-214 - Mutfak ekranında 409 çakışma tespiti fiilen hiç çalışmıyordu

- Task ID: V1-RMD-214
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) bulduğu **CRITICAL** bulgu:
`KitchenOperationsWorkspace.tsx`'in `isConflict()` yardımcı fonksiyonu
`error.message`'ı İngilizce bir regex'le (`/409|conflict|concurrent|
version/i`) test ediyordu, ama backend her zaman Türkçe mesaj
döndürüyor ("Kayıt başka bir işlem tarafından değiştirildi.") — bu
regex ASLA eşleşmiyordu. Ayrıca her çağrı noktasındaki
`error instanceof ApiError` düşüşü, `../../api`'den içe aktarılan
tamamen ilişkisiz bir sınıfı kontrol ediyordu; bu ekranın gerçek hata
sınıfı `kitchenApi.ts`'deki `KitchenOperationsApiError`. Sonuç: gerçek
bir 409 çakışmasında bile kullanıcı hep aynı jenerik statik mesajı
görüyordu, "conflict" tonu hiç tetiklenmiyordu, ve elindeki eski
kartla işleme devam edebiliyordu.

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx
  (ilgili modülün sahipliğinde)
- src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.test.tsx
  (aynı modül)

## In scope

1. `isConflict(error)`: artık `error instanceof KitchenOperationsApiError
   && error.status === 409` — backend'in kendi 409 kodlarının hepsi
   (CONCURRENT_MODIFICATION, DOMAIN_CONFLICT, DUPLICATE_RESOURCE,
   RESOURCE_IN_USE) bu ekran için aynı anlama geliyor: kullanıcının
   baktığı kart artık güncel değil.
2. `../../api`'den `ApiError` içe aktarımı kaldırıldı; 5 çağrı
   noktasındaki `error instanceof ApiError` düşüşü
   `error instanceof KitchenOperationsApiError` (zaten `models.ts`
   içe aktarımının yanına eklenen `kitchenApi.ts` içe aktarımı) oldu.
3. Yeni test: gerçek bir `KitchenOperationsApiError(409, ...)` fırlatan
   bir `onTransitionItem` mock'u, geri bildirimin `conflict` tonunu ve
   doğru Türkçe mesajı gösterdiğini kanıtlıyor.

## Out of scope

- Backend'in 409 kod sözlüğü (`KitchenOperationsEndpoints.cs`) —
  zaten doğru, dokunulmadı.

## Dependencies

- V1-KDS-001

## Acceptance evidence

- `npx tsc --noEmit` (PosTerminal) → 0 hata.
- `npx vitest run` (PosTerminal, tüm proje) → 170/170 yeşil, yeni test
  dahil; revert-and-confirm ile testin gerçekten düzeltmeye bağlı
  olduğu kanıtlanır.

## Handoff

- None

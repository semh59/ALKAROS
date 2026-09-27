# V1-RMD-367 - Masa (Tables) modülü yeniden açma: workspace.tsx rota seviyesinde yanlış hata sınıfı

- Task ID: V1-RMD-367
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

`V1-RMD-365` bu modülü ("PosTerminal — workspace.tsx + tables", Modül 6) "gerçek bulgu yok"
diye kapatmıştı. `V1-RMD-366`'yı (Modül 7, billing) kapatırken bulunan sistemik "kendi API hata
sınıfını fırlatan istemci, yalnızca paylaşılan `ApiError`'ı kontrol eden bileşen" deseni
kontrol edildiğinde, bu deseninin `routes/workspace.tsx`'in `TableRoute.load()` ve
`handleSaveFloorPlan()` fonksiyonlarında da GERÇEKTEN var olduğu görüldü —
`TableWorkspace.tsx`'in kendi (V1-RMD-114'te zaten doğru düzeltilmiş) `errorMessage()` yardımcı
fonksiyonuyla karıştırılıp, `workspace.tsx`'in ayrı rota-seviyesi `load()`'u kontrol edilmemişti.

Kuralın gereği ("bulunan sorunlar çözülmeden diğerine geçmek yok") gereği modül yeniden açıldı,
düzeltildi ve tekrar kapatıldı — bu, sürecin kendi dürüstlük disiplininin bir parçası: bir modül
"Tamamlandı" olarak işaretlendikten SONRA gerçek bir bulgu ortaya çıkarsa, ileri modüllere devam
etmeden önce geri dönülür.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-367-posterminal-tables-error-class-fix.md`

## In scope

1. **[T3/T6, Yüksek — hata yönetimi/frontend-backend uyumu] `TableRoute.load()` ve
   `handleSaveFloorPlan()` yalnızca `reason instanceof ApiError` kontrol ediyordu**, ama
   `tableApi.ts`'nin gerçek istemcisi kendi `TableManagementApiError`'ını fırlatıyor. Her ikisi
   de `reason instanceof ApiError || reason instanceof TableManagementApiError` şeklinde
   düzeltildi.

## Out of scope

- `mutate()` (createZone/createTable/onAction) etkilenmedi — hatalar zaten `TableWorkspace.tsx`'in
  kendi doğru `errorMessage()` yardımcısına kadar yayılıyor.

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (37
  dosya, 272 test, bu görevin yeni testi dahil): 272/272 geçti, regresyon yok.
- Mutation-check: `workspace.tsx` `git stash` ile geri alındı, yeni test GERÇEKTEN kırmızı oldu
  (jenerik "Masa verisi alınamadı." metni gösterildi, sunucunun gerçek mesajı değil). `git stash
  pop` ile geri yüklendi, paket tekrar 272/272 yeşile döndü.

## Handoff

- None

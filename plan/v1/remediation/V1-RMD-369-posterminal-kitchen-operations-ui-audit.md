# V1-RMD-369 - PosTerminal Mutfak (Kitchen Operations) modül denetimi: ARIA durumu + yanlış hata sınıfı

- Task ID: V1-RMD-369
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 9. modülü:
PosTerminal'in Mutfak/Operasyon ekranı (`src/Clients/PosTerminal/src/features/kitchen-operations/**`,
~1930 satır) ve onu bağlayan `routes/workspace.tsx`'in `KitchenRoute` parçası. On iki boyut
üzerinden tarandı.

Bu dosya genel olarak çok olgun (V1-RMD-200/214/218/220/223/225 gibi önceki bağımsız denetim
turlarından geçmiş — `KitchenOperationsWorkspace.tsx`'in kendi aksiyon işleyicileri
`KitchenOperationsApiError`'ı zaten doğru kontrol ediyordu, V1-RMD-214), ama iki gerçek bulgu
vardı:

1. **Görünüm modu (Expo/Tüm Gün/Rapor) ve ekran yoğunluğu (Otomatik/Sakin/Yoğun) düğme
   grupları yalnızca görsel `.is-active` sınıfı taşıyordu, hiç ARIA durumu yoktu** — Cashier
   vanilla'nın (Modül 1) ve Mutfak'ın kendi masa yönetiminin (`TableWorkspace.tsx`) zaten
   kapattığı aynı sınıftan bulgu.
2. **`workspace.tsx`'in `KitchenRoute.load()`'u yalnızca `reason instanceof ApiError` kontrol
   ediyordu**, ama `kitchenApi.ts`'nin gerçek istemcisi kendi `KitchenOperationsApiError`'ını
   fırlatıyor — V1-RMD-366/367/368 ile aynı sistemik desen, bu kez route-seviyesinde (aksiyon
   işleyicileri zaten doğruydu, yalnızca ilk yükleme hatası etkilenmişti).

Ayrıca, kalem durumu göstergesi (`kitchen-stepper`) mevcut aşamayı yalnızca görsel bir sınıfla
(`is-current`) işaretliyordu; `aria-current="step"` eklendi — bir sıralı-aşama göstergesi olduğu
için (bir toggle grubu DEĞİL) `aria-pressed` yerine bu, WAI-ARIA'nın kendi "step" değeri
kullanıldı.

`SystemHealthRoute` (Modül 12) ve "authorization" rotasının `workspace.tsx`'teki AYNI deseni
bilinçli olarak bu görevin dışında bırakıldı — kendi modüllerine varıldığında ele alınacak.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-369-posterminal-kitchen-operations-ui-audit.md`

## In scope

1. **[T1, Orta] Görünüm modu/ekran yoğunluğu düğme grupları `aria-pressed` taşımıyordu.**
   `TableWorkspace.tsx`'in zaten kurduğu aynı `role="group"` + `aria-pressed` deseniyle
   düzeltildi (tutarlılık için `role="tab"`/radiogroup değil).
2. **[T1, Düşük] Kalem durumu göstergesindeki mevcut aşama yalnızca görsel işaretliydi.**
   `aria-current="step"` eklendi.
3. **[T3/T6, Yüksek — hata yönetimi/frontend-backend uyumu] `KitchenRoute.load()` yalnızca
   `reason instanceof ApiError` kontrol ediyordu.** `reason instanceof ApiError || reason
   instanceof KitchenOperationsApiError` şeklinde düzeltildi.

## Out of scope

- `SystemHealthRoute`/authorization rotalarındaki aynı hata-sınıfı deseni ertelendi (Modül 12 ve
  modül listesinde açıkça yer almayan authorization alanı).
- Ürün-katmanı (P1-P4) gözlemi yok: mutfak ekranı zaten bu oturumun en yoğun bağımsız denetim
  geçmişine sahip dosyalarından biri.

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (37
  dosya, 275 test, bu görevin 3 yeni testi dahil): 275/275 geçti, regresyon yok.
- Mutation-check: `workspace.tsx` + `KitchenOperationsWorkspace.tsx` `git stash` ile geri
  alındı, yeni üç test de GERÇEKTEN kırmızı oldu. `git stash pop` ile geri yüklendi, paket
  tekrar 275/275 yeşile döndü.

## Handoff

- None

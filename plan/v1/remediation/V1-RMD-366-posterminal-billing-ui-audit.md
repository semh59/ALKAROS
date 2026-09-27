# V1-RMD-366 - PosTerminal Billing (Hesap Bölme) modül denetimi: yanlış hata sınıfı kontrolü

- Task ID: V1-RMD-366
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 7. modülü:
PosTerminal'in Hesap Bölme (Billing/Split) ekranı
(`src/Clients/PosTerminal/src/features/billing/**`, ~520 satır) ve onu bağlayan
`routes/workspace.tsx`'in `BillingRoute` parçası. On iki boyut üzerinden tarandı.

Bu dosya da (Modül 5/6 gibi) genel olarak olgun çıktı: `role="tablist"`/`"tab"`+`aria-selected`
dağıtım modu seçicisinde ZATEN doğru kullanılıyor (Modül 5'in kategori-rayı bulgusundaki eksiklik
burada yok); `role="status"`/`"alert"` geri bildirim kutularında zaten var; `ValidationSummary`
paylaşılan bileşeni kullanılıyor; sistematik CSS/JS sınıf adı taraması sıfır eksik buldu.

Tek ama gerçek ve sistemik bir bulgu: **`billingApi.ts` kendi hata sınıfını (`BillingSplitApiError`)
fırlatıyor, paylaşılan `ApiError`'ı DEĞİL** — ve üç ayrı yerde (`routes/workspace.tsx`'in
`BillingRoute.load()`'u, `BillSplitWorkspace.tsx`'in `save()` ve `clear()`'ı) yalnızca
`reason instanceof ApiError` kontrol ediliyordu. Bu, `TableWorkspace.tsx`/`tableApi.ts` çiftinde
V1-RMD-114'ün (2026-09-06 bağımsız denetim) zaten kapattığı AYNI hata sınıfı — ama düzeltme
zamanında billing modülüne hiç uğramamış. Sonuç: gerçek bir sunucu hatası (bir doğrulama
reddi, kilitli hesap durumu, vb.) sessizce jenerik bir yedek metinle değiştiriliyor, sunucunun
kendi Türkçe gerekçesi kasiyerden/yöneticiden gizleniyordu.

Bunun neden mevcut testten kaçtığı da bulundu: `BillSplitWorkspace.test.tsx`'in V1-RMD-114
regresyon testi `onSave`'i `new ApiError(...)` ile reddediyordu — üretim kodunun gerçekte
fırlattığı sınıfı (`BillingSplitApiError`) hiç sınamıyordu, bu yüzden `instanceof` kontrolündeki
gerçek boşluk hiç kırmızı olmadı.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-366-posterminal-billing-ui-audit.md`

## In scope

1. **[T3/T6, Yüksek — hata yönetimi/frontend-backend uyumu] Üç çağrı noktası
   (`workspace.tsx`'in `BillingRoute.load()`'u, `BillSplitWorkspace.tsx`'in `save()`/`clear()`'ı)
   yalnızca `reason instanceof ApiError` kontrol ediyordu**, ama `billingApi.ts`'nin gerçek
   istemcisi (`createBillingSplitClient`/`createBillFromOrder`) kendi `BillingSplitApiError`
   sınıfını fırlatıyor. Her üçü de `reason instanceof ApiError || reason instanceof
   BillingSplitApiError` şeklinde düzeltildi — `tableApi.ts`'nin `TableManagementApiError`'ı için
   `TableWorkspace.tsx`'in zaten yaptığı aynı iki-sınıflı kontrol.

## Out of scope

- Bu modülde ek bir ürün-katmanı (P1-P4) gözlemi bulunmadı: eşit/ürün/tutar üç dağıtım modu
  rakip ürünlerle (Toast'un "split by item/seat/even") kıyaslanabilir, öğrenme eşiği düşük
  (sekmeler arası geçiş net etiketli).

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (37 dosya,
  269 test, iki yeni test dahil): 269/269 geçti, regresyon yok.
- Mutation-check: `workspace.tsx` + `BillSplitWorkspace.tsx` `git stash` ile geri alındı, YENİ
  iki test de GERÇEKTEN kırmızı oldu (ikisi de jenerik yedek metni gösterdi, sunucunun gerçek
  mesajını değil). `git stash pop` ile geri yüklendi, paket tekrar 269/269 yeşile döndü.
- Yeni `workspace.test.tsx` testi, ilk yazımda yanlışlıkla 409 durum kodunu kullandı — bu, mesajı
  hiç göstermeyen "stale" durumuna denk geldiği için testi kendi hatasıyla yanlış şekilde kırmızı
  yaptı (kasıtlı bir tasarım — "hesap güncel değil" durumunda spesifik metin değil, "güncel veri
  al" mesajı gösteriliyor). 422 durum koduna değiştirilerek asıl `ApiError`/`BillingSplitApiError`
  ayrımını sınayan doğru "error" durumuna yönlendirildi.

## Handoff

- None

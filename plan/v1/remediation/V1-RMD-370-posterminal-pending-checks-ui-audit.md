# V1-RMD-370 - PosTerminal Bekleyen Hesaplar modül denetimi: axe taraması eksikliği

- Task ID: V1-RMD-370
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 10. modülü:
PosTerminal'in Bekleyen Hesaplar (garsonun kasaya gönderdiği, tahsil edilmeyi bekleyen hesaplar)
ekranı (`src/Clients/PosTerminal/src/features/pending-checks/**`, ~386 satır). On iki boyut
üzerinden tarandı.

Bu dosya zaten çok olgun: `PendingChecksApiError` doğru kontrol ediliyor; `collect()`'in
`createBillFromOrder` (billing) çağrısı `instanceof Error` ile daha geniş bir şekilde
yakalanıyor (bu, `BillingSplitApiError`'ı da kapsıyor — `Error`'dan türediği için — bu yüzden
Modül 7/8/9'daki dar `instanceof ApiError` boşluğu burada YOK); canlı SignalR bağlantısı
Cashier.tsx'in kendi yardım-çağrısı bağlantısıyla aynı sonsuz-yeniden-deneme kuralına sahip;
`aria-label`'lar her satırın ne yaptığını net anlatıyor; `role="alert"` hata metninde zaten var;
`PendingChecksWorkspace.tsx` route seviyesinde ayrı bir `workspace.tsx` sarmalayıcısı yok
(kendi state'ini kendi yönetiyor), bu yüzden Modül 7/8/9'un route-seviyesi hata sınıfı bulgusu
burada uygulanamaz.

Tek gerçek bulgu: Modül 5'teki (`Cashier.tsx`) ile aynı sınıftan bir test-kapsama boşluğu —
`PendingChecksWorkspace.tsx`'in hiçbir axe-core taraması yoktu, ~13 diğer feature workspace'inin
(`CatalogWorkspace`, `BillSplitWorkspace`, `TableWorkspace`, vb.) her birinin kendi "has no
critical or serious axe violations" testi zaten var.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-370-posterminal-pending-checks-ui-audit.md`

## In scope

1. **[T1, Yüksek — test kapsamı] `PendingChecksWorkspace.tsx` hiçbir axe-core taramasından
   geçmiyordu.** Aynı `axe.run(document, { rules: { "color-contrast": { enabled: false } } })`
   deseniyle yeni bir test eklendi.

## Out of scope

- Üretim kodu değişmedi — yalnızca test-kapsama boşluğu kapatıldı.
- Ürün-katmanı (P1-P4) gözlemi yok.

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (37
  dosya, 276 test, bu görevin yeni testi dahil): 276/276 geçti, regresyon yok.
- Üretim kodu değişmediği için mutation-check gerekmedi (yalnızca yeni bir test dosyası eklendi,
  geri alınacak bir düzeltme yok — Modül 6'nın "no findings" kapanışında olduğu gibi).
- Yeni test ilk yazımda `document-title` axe kuralına takıldı (varsayılan test ortamının boş
  `<title>`'ı); diğer axe testleriyle aynı `document.title` ayarı eklenerek düzeltildi.

## Handoff

- None

# V1-RMD-386 - PosTerminal online-* Tur 2 denetimi: yeni online sipariş için sesli uyarı yoktu

- Task ID: V1-RMD-386
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 11: PosTerminal'in
Online Yemek bölümü. Bu bölüm zaten hem `OnlineFoodHub` (60s) hem `OnlineOperationsWorkspace`
(20s) seviyesinde poll taşıyordu — T7 zaten iyiydi. Kitchen'ın (Modül 9, V1-RMD-384) bulduğu aynı
sınıftan bir P1/P2 boşluğu burada da vardı: sessiz bir kuyruk.

## Owned surface

- `src/Clients/PosTerminal/src/audioAlerts.ts`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-operations/OnlineOperationsWorkspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-operations/OnlineOperationsWorkspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-386-online-operations-round2-audit.md`

## In scope

1. **[P1/P2, Yüksek — rakip karşılaştırması/saha gerçekliği] Yeni bir QR/online sipariş
   geldiğinde hiçbir sesli uyarı yoktu.** Bir online platform siparişi zamanında kabul/red
   edilmezse platformun kendi SLA sayacı yine de işliyor — personel salonda bir müşteriyle
   meşgulken veya mutfak hattında yoğunken, bu ekrana bakmadıkça yeni bir siparişin geldiğini
   öğrenemiyordu. Kitchen'ın Tur 2'deki düzeltmesiyle (V1-RMD-384) AYNI Web Audio API sentez
   deseni kullanıldı — bu kez iki dosyada tekrarlanmaması için paylaşılan bir
   `src/Clients/PosTerminal/src/audioAlerts.ts` modülüne çıkarıldı, `KitchenOperationsWorkspace`
   de bu paylaşılan modülü kullanacak şekilde yeniden düzenlendi (davranışta değişiklik yok,
   yalnızca kod tekrarını önlemek için).

## Out of scope

- P4/T7/T8: bu bölümün geri kalanı zaten çok olgun (Tur 1'de "en olgun bölümlerden biri" olarak
  belgelenmişti); ek bulgu yok.

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (39
  dosya, 296 test, bu görevin yeni testi dahil): 296/296 geçti, regresyon yok.
- Mutation-check: yalnızca `OnlineOperationsWorkspace.tsx` `git stash` ile geri alındı
  (`audioAlerts.ts` ve refactor edilmiş `KitchenOperationsWorkspace.tsx` YERİNDE bırakıldı) —
  yeni test GERÇEKTEN kırmızı oldu, Kitchen'ın kendi testi ise YEŞİL kaldı (paylaşılan modüle
  taşımanın Kitchen'ı bozmadığının kanıtı). `git stash pop` ile geri yüklendi, tam paket tekrar
  296/296 yeşile döndü.

## Handoff

- None

# V1-RMD-384 - PosTerminal kitchen-operations Tur 2 denetimi: yeni bilet için sesli uyarı yoktu

- Task ID: V1-RMD-384
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 9: PosTerminal'in
Mutfak/Operasyon (Kitchen Display System) ekranı. Bu modül zaten en olgun T7 örneğine sahipti
(8 saniyelik canlı poll — Tables'ın Tur 2'de kopyaladığı desen), ama gerçek bir P1/P2 boşluğu
vardı: sessiz bir ekran.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-384-kitchen-operations-round2-audit.md`

## In scope

1. **[P1/P2, Yüksek — rakip karşılaştırması/saha gerçekliği] Yeni bir bilet geldiğinde hiçbir
   sesli uyarı yoktu.** Her gerçek ticari KDS (Toast KDS, QSR Automations, Fresh KDS) yeni bir
   bilet düştüğü an sesli bir uyarı verir — mutfak personeli vardiyanın çoğunda elleri dolu ve
   sırtı ekrana dönük çalışır, bir kasiyerin aksine sürekli ekrana bakmaz. Bu pano yalnızca
   sessiz bir 8 saniyelik poll yapıyordu; yeni bir bilet ancak biri şans eseri rafa bakınca fark
   ediliyordu. Web Audio API ile (harici bir ses dosyası paketlemeden) iki kısa ton
   sentezlenerek, panonun daha önce hiç görmediği bir bilet ID'si belirdiğinde çalınıyor —
   ekran ilk açıldığında (vardiya ortasında başlayan, zaten kuyrukta bekleyen biletler için
   ASLA), yalnızca poll'un GERÇEKTEN yeni bir bilet getirdiği an.

## Out of scope

- P4/T7/T8: bu modülün geri kalanı Tur 1'de (V1-RMD-369) zaten derinlemesine incelenmişti; T7
  zaten bu turun diğer modüllerinin kopyaladığı referans desendi.

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (39
  dosya, 295 test, bu görevin yeni testi dahil): 295/295 geçti, regresyon yok.
- Mutation-check: `KitchenOperationsWorkspace.tsx` `git stash` ile geri alındı, yeni test
  GERÇEKTEN kırmızı oldu (yeni bilet geldiğinde `AudioContext` hiç çağrılmadı). `git stash pop`
  ile geri yüklendi, paket tekrar 295/295 yeşile döndü.
- Test, gerçek ses çıkışını değil, `AudioContext`'in DOĞRU KOŞULLARDA (yalnızca gerçekten yeni
  bir bilet ID'si belirdiğinde, ilk yüklemede veya değişmeyen bir yeniden render'da DEĞİL)
  çağrıldığını mock'layarak doğruluyor.

## Handoff

- None

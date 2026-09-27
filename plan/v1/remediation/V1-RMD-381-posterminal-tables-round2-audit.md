# V1-RMD-381 - PosTerminal workspace.tsx + tables Tur 2 denetimi: masa durumu sessizce bayatlıyordu

- Task ID: V1-RMD-381
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 6:
`routes/workspace.tsx` + `features/tables/**`. Tur 1'de bu modül kod tabanının en olgun köşesi
olarak kapanmıştı (klavye eşdeğerli kat planı düzenleyici, erişilebilir liste görünümü). Tur 2,
gerçek bir T7 (rol-arası haberleşme) boşluğu buldu: masa ekranı hiçbir zaman kendiliğinden
yenilenmiyordu.

Ayrıca araştırılan ama BÜYÜK KAPSAMLI bir backend özelliği gerektirdiği için kod değişikliği
yapılmadan not edilen bir P1 gözlemi: bu kod tabanında hiçbir "bekleme listesi" (waitlist)
kavramı yok (rakip ürünlerin çoğu — Toast, Square — dolu masalarda bekleyen misafirler için bu
özelliği sunar). Yeni bir domain kavramı + kalıcılık + uç nokta + arayüz gerektiriyor; Semih'in
kararına bırakıldı.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-381-posterminal-tables-round2-audit.md`

## In scope

1. **[T7, Yüksek — rol-arası haberleşme] Masa ekranı kendiliğinden hiç yenilenmiyordu.**
   `KitchenOperationsWorkspace`'in kendisi zaten 8 saniyede bir poll yapıyor ("the comparison
   report's own CRIT finding" yorumuyla belgelenmiş bir önceki bulgu), ama masa ekranında bu
   YOKTU — bir yönetici kat planına bakarken, bir garsonun/kasiyerin yaptığı masa durumu
   değişikliği (dolu, temizlendi, devredildi) yalnızca elle "Yenile" tıklanınca görünüyordu.
   Kitchen'ın kendi deseni (`state === "ready" || state === "empty"` iken, hata/yetkisiz/
   çevrimdışı durumlarda değil) birebir mirror edilerek 8 saniyelik bir poll eklendi. Kat planı
   DÜZENLEME modunda (`FloorPlanWorkspace`'in kendi `mode === "setup"`) güvenli — o bileşenin
   kendi `useEffect`'i taslağı yalnızca "operation" modundayken sunucudan gelen taze `plan`
   prop'uyla yeniden eşitliyor.

## Out of scope

- **[P1, rakip karşılaştırması — büyük kapsamlı] Bekleme listesi (waitlist) kavramı yok.** Yeni
  bir backend domain kavramı, kalıcılık ve uç nokta gerektiriyor — bu görevin kapsamının
  ötesinde, Semih'in önceliklendirmesine bırakıldı.
- P2/P4/T8: Tur 1'de zaten derinlemesine incelenmişti, ek bulgu yok.

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (39
  dosya, 294 test, bu görevin yeni testi dahil): 294/294 geçti, regresyon yok.
- Mutation-check: `workspace.tsx` `git stash` ile geri alındı, yeni test GERÇEKTEN kırmızı oldu
  (8 saniye ilerletildikten sonra ikinci bir istek hiç atılmadı). `git stash pop` ile geri
  yüklendi, paket tekrar 294/294 yeşile döndü.

## Handoff

- None

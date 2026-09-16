# V1-RMD-222 - Bilet seviyesinde manuel "Preparing" geçişi artık kalem durumunu doğruluyor

- Task ID: V1-RMD-222
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) bulduğu **MEDIUM** bulgu:
`KitchenTicket.CanTransitionTo`'nun `(Accepted, Preparing)` dalı hiçbir
kalem-durumu kontrolü yapmıyordu — `(Preparing, Ready)`'nin kendi
`CanBeMarkedReady()` korumasının aksine. `kitchen.advance` yetkisine
sahip bir çağıran (gerçek PosTerminal istemcisi bunu hiç göndermiyor,
ama genel `POST /tickets/{id}/transition` ucu bunu kabul ediyor),
hiçbir kalem `Preparing`'e geçmeden tüm bileti `Preparing` yapabiliyordu
— bilet durumu ile gerçek kalem durumları kalıcı olarak
çelişebiliyordu.

## Owned surface

- src/Modules/Kitchen/TicketLifecycle/KitchenTicket.cs (ilgili modülün
  sahipliğinde)
- tests/Modules/Kitchen/TicketLifecycle/KitchenTicketTests.cs (aynı
  modül)

## In scope

1. Yeni `CanBeMarkedPreparing()`: `CanBeMarkedReady()` ile aynı
   desende — iptal edilmemiş kalemlerin en az biri artık `Queued`
   değilse true. `(Accepted, Preparing)` artık bunu şart koşuyor.
2. Gerçek yol (V1-KIT-007'nin `UpdateItemStatus` içindeki otomatik
   terfisi — bir kalem `Preparing`'e geçtiğinde bilet de otomatik
   `Preparing` olur) değişmedi, hâlâ hiçbir korumaya takılmıyor.
3. Mevcut `TicketLifecycleRoundTripsThroughPostgres` testi, artık var
   olmayan geçersiz kalıbı (manuel `TransitionTo(Preparing)`, hiçbir
   kalem henüz `Preparing` değilken) gerçek kullanım şekline
   (`UpdateItemStatus` ile otomatik terfi) güncellendi.
4. Yeni birim testi: hiçbir kalem başlamamışken manuel geçiş
   reddediliyor; gerçek yol (item transition) hâlâ çalışıyor.

## Out of scope

- Genel `POST /tickets/{id}/transition` ucunun kendisi — zaten
  `ticket.TransitionTo` üzerinden domain kuralına uyuyor, ayrı bir
  değişiklik gerekmedi.

## Dependencies

- V1-KIT-007

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı `tests/Modules/Kitchen/TicketLifecycle` →
  30/30 yeşil (yeni test dahil, mevcut round-trip testi güncellendi);
  revert-and-confirm ile gerçekten kırılıp doğrulandı.
- Regresyon taraması: `tests/Host/Experience/KitchenOperations` →
  34/34 yeşil.

## Handoff

- None

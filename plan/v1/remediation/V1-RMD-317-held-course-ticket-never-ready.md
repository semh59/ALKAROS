# V1-RMD-317 - Çok kurslu siparişlerde mutfak fişi asla "Hazır" olamıyordu

- Task ID: V1-RMD-317
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K5 bulgusu: `Order.FireRound` bir turun sonraki kurs kalemlerini de (`CourseNumber` ilk kurstan farklı) ateşlenen listeye ekliyor, `KitchenTicket.CreateFromOrder` bunları `Queued`/`IsHeld:true` olarak fişe yazıyordu. `Order.FireCourse` bu kalemi bu fişte İLERLETMİYOR — `OrderSubmissionCoordinator.FireCourseAsync` o kurs için tamamen YENİ, ayrı bir `KitchenTicket` açıp gönderiyor. Sonuç: orijinal fişin kendi `IsHeld` kalemi sonsuza kadar `Queued` kalıyor; `KitchenTicket.CanBeMarkedReady()` (ve `UpdateItemStatus`'un kendi ayrı, tekrarlanmış "auto-ready" kontrolü) bunu diğer tüm kalemler `Ready`/`Served` olsa bile fişi asla `Ready`'ye taşımayacak şekilde engelliyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-317-held-course-ticket-never-ready.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Kitchen/TicketLifecycle/KitchenTicket.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Kitchen/TicketLifecycle/KitchenTicketTests.cs

## In scope

1. `CanBeMarkedReady()`: `IsHeld` kalemleri, `Cancelled` kalemler gibi hesap dışı tutulur.
2. `UpdateItemStatus`'un kendi ayrı "auto-ready" kontrolü (aynı mantığın bağımsız bir tekrarı): aynı şekilde `IsHeld` kalemleri hesap dışı tutar.

## Out of scope

- "Auto-cancel" kontrolü (`newItems.All(i => i.Status == Cancelled)`) — bu bulgunun tarif ettiği semptomla ilgisiz, dokunulmadı.
- `Order.FireCourse`'un mimarisi (yeni ayrı fiş açması) — kendi başına yanlış değil, yalnız orijinal fişin bunu yansıtmaması sorun; mimariyi değiştirmek yerine orijinal fişin kendi tamlık hesabını düzeltmek seçildi (daha küçük, daha güvenli değişiklik).

## Dependencies

- None

## Acceptance evidence

Yeni bir birim testi (`ParentReadyIgnoresAHeldLaterCourseItemThatStaysQueuedForever`, gerçek `KitchenTicket`/`KitchenTicketItem` domain nesneleriyle): bir turun ilk kursu (`Queued`) ile aynı fişteki `IsHeld:true` ikinci kurs kalemi seed edilir; ilk kurs kalemi `Preparing`→`Ready`'ye ilerletilir, ikinci kurs kalemi bilerek hiç ilerletilmez (gerçek davranışı taklit eder — o kalem ayrı bir fişte ilerler). `CanBeMarkedReady()` `true` döner ve fiş gerçekten `Ready`'ye otomatik geçer. Tüm ilgili test projeleri çalıştırıldı, regresyon yok: `ALKAROS.Kitchen.TicketLifecycle.Tests` 31/31 (1 yeni), `ALKAROS.Host.Experience.KitchenOperations.Tests` 35/35, `ALKAROS.Host.Experience.Orders.Confirmation.Tests` 30/30, `ALKAROS.Host.Experience.Orders.VoidSent.Tests` 14/14, `ALKAROS.Kitchen.PrintQueue.Tests` 27/27, `ALKAROS.Kitchen.PhysicalPrintRecovery.Tests` 17/17.

Mutasyon kontrolü: her iki `!i.IsHeld` filtresi geçici olarak kaldırıldı — yeni test gerçekten kırmızı oldu (önce `t2.Status` beklenenden farklı, filtre ikisi de kaldırılınca `CanBeMarkedReady()` `false` döndü); dosya `diff` ile birebir orijinaline geri getirildi, tüm paket tekrar yeşil.

## Handoff

- None

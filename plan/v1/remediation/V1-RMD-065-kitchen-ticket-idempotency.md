# V1-RMD-065 - Kitchen ticket idempotency

- Task ID: V1-RMD-065
- Status: Done
- Assignee: 0e4d2094-eff5-490e-9402-056e17c86376
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`KitchenTicket.TransitionTo` ve `KitchenOperationsStore.TransitionTicketAsync` içinde bilet durumu geçişlerini idempotent hale getirerek, otomatik terfi sonrasında gelen geçiş komutlarının 409 Conflict vermesini engellemek.

## Owned surface

- `plan/v1/remediation/V1-RMD-065-kitchen-ticket-idempotency.md`
- PO:2026-08-31 kararıyla src/Modules/Kitchen/TicketLifecycle/**, src/Host/Experience/KitchenOperations/**, tests/Modules/Kitchen/TicketLifecycle/** ve tests/Host/Experience/KitchenOperations/** yüzeyleri V1-RMD-074'e devredildi; bu historical task closed kalır.

## In scope

- `KitchenTicket.TransitionTo` içerisinde bilet zaten hedef durumda ise idempotent olarak `this` döndürmek.
- `KitchenOperationsStore.TransitionTicketAsync` içerisinde durum doğrulamalarını güncellemek.
- `TicketLifecycleRoundTripsThroughPostgres` ve `TicketItemLifecycleUsesAuthoritativeVersionsAndRejectsStaleWrites` testlerinin 0 hata ile geçmesini sağlamak.

## Out of scope

- Diğer mutfak yazdırma/kuyruk davranışlarını değiştirmek.

## Dependencies

- V1-RMD-064

## Deliverables

- Idempotent mutfak bileti durum geçişleri ve yeşil geçen entegrasyon testleri.

## Acceptance evidence

- `dotnet test tests/Modules/Kitchen/TicketLifecycle/ALKAROS.Kitchen.TicketLifecycle.Tests.csproj` 0 hata verir.
- `dotnet test tests/Host/Experience/KitchenOperations/ALKAROS.Host.Experience.KitchenOperations.Tests.csproj` 0 hata verir.

## Handoff

- V1-RMD-066

# V1-RMD-062 - Kitchen ticket auto ready promotion

- Task ID: V1-RMD-062
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`KitchenTicket.cs` içinde tüm aktif kalemler hazır (`Ready`) olduğunda fiş başlığının otomatik olarak `Ready` durumuna geçmesini sağlamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-062-kitchen-ticket-auto-ready-promotion.md`
- PO:2026-08-31 kararıyla src/Modules/Kitchen/TicketLifecycle/** ve tests/Modules/Kitchen/TicketLifecycle/** yüzeyleri V1-RMD-065'e devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-062/**`

## In scope

- `KitchenTicket.cs` içinde `UpdateItemStatus` metodunda tüm kalemler hazır olduğunda fiş durumunu `KitchenTicketStatus.Ready` olarak güncellemek.
- Durum geçişi için xUnit test senaryosu eklemek.

## Out of scope

- Diğer mutfak modüllerini değiştirmek.

## Dependencies

- V1-RMD-061

## Deliverables

- Kalemler tamamlandığında otomatik `Ready` durumuna geçen mutfak fişi domain modeli.

## Acceptance evidence

- `dotnet test tests/Modules/Kitchen/TicketLifecycle/ALKAROS.Kitchen.TicketLifecycle.Tests.csproj` 0 hata verir.

## Handoff

- V1-GOV-035

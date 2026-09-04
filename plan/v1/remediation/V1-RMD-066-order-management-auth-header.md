# V1-RMD-066 - Order management auth header

- Task ID: V1-RMD-066
- Status: Done
- Assignee: 0e4d2094-eff5-490e-9402-056e17c86376
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`OrderManagementEndpoints.cs` içerisinde `RequireCashierSessionAsync` metoduna `Authorization: Bearer <sessionToken>` başlık kontrolü ekleyerek Garson PWA isteklerinin 401 Unauthorized ile reddedilmesini önlemek.

## Owned surface

- `plan/v1/remediation/V1-RMD-066-order-management-auth-header.md`
- PO:2026-09-05 kararıyla src/Host/Experience/Orders/OrderManagementEndpoints.cs yüzeyi izin kodu yeniden eşleme için V1-IAM-024'e devredildi; bu historical task closed kalır.
- PO:2026-08-31 kararıyla src/Clients/WaiterPwa/wwwroot/waiter-app.js yüzeyi V1-RMD-079'a devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-066/**`

## In scope

- `RequireCashierSessionAsync` içinde çerez yoksa `context.Request.Headers.Authorization` başlığından token okumak.
- Bearer token ile çalışan sipariş doğrulama testleri eklemek.

## Out of scope

- Diğer kimlik doğrulama filtrelerini değiştirmek.

## Dependencies

- V1-RMD-065

## Deliverables

- Hem çerez hem de Authorization başlığı destekleyen `OrderManagementEndpoints.cs`.

## Acceptance evidence

- Hem Cookie hem de Bearer token ile gelen masa sipariş taslak istekleri yetkilendirmeden geçer.

## Handoff

- V1-RMD-067

# V1-RMD-047 - Cashier waiter fail closed mock cleanup

- Task ID: V1-RMD-047
- Status: InProgress
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

Kasiyer ve Garson PWA arayüzlerindeki sahte başarı, mock katalog/masa fallback verileri ve yanıltıcı bildirimleri fail-closed prensibiyle tamamen temizlemek.

## Owned surface

- `plan/v1/remediation/V1-RMD-047-cashier-waiter-fail-closed-mock-cleanup.md`
- `src/Clients/Cashier/wwwroot/**`
- `src/Clients/WaiterPwa/wwwroot/waiter-app.js`
- `evidence/V1-RMD-047/**`

## In scope

- `cashier-app.js` içindeki backend çağrısı olmayan sahte mutfağa iletildi ve lokal state manipülasyonlarını temizlemek.
- `waiter-app.js` içindeki API hata durumlarında devreye giren sabit fallback verileri kaldırarak kullanıcıya net ve gerçek hata durumunu göstermek.

## Out of scope

- C# backend veya PosTerminal React istemcisini değiştirmek.

## Dependencies

- V1-RMD-046

## Deliverables

- Mock ve fallback içermeyen, kesin fail-closed istemci kodları.

## Acceptance evidence

- İstemciler API bağlantısı olmadan sahte veri ve sahte başarı üretmez.

## Handoff

- V1-RMD-048

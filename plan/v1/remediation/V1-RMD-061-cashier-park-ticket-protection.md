# V1-RMD-061 - Cashier park ticket protection

- Task ID: V1-RMD-061
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`cashier-app.js` içinde bekletilen fiş geri yüklenirken aktif sepetteki kalemlerin kazara silinmesini engellemek ve hızlı satış masası gönderimini doğrulamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-061-cashier-park-ticket-protection.md`
- `src/Clients/Cashier/wwwroot/**`
- `evidence/V1-RMD-061/**`

## In scope

- `recallParkedTicket` işleminde mevcut sepet kontrolü ve kullanıcı onayı eklemek.
- Hızlı satış fişi gönderimini sağlamlaştırmak.

## Out of scope

- Kasa donanım sürücülerini değiştirmek.

## Dependencies

- V1-RMD-060

## Deliverables

- Sepet ezilmesini önleyen bekletme/geri yükleme akışı.

## Acceptance evidence

- `cashier-app.js` içinde aktif sepet doluyken fiş geri yükleme onay ister.

## Handoff

- V1-RMD-062

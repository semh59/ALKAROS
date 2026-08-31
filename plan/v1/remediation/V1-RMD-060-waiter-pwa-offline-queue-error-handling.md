# V1-RMD-060 - Waiter pwa offline queue error handling

- Task ID: V1-RMD-060
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`waiter-app.js` çevrimdışı senkronizasyon kuyruğunda sunucu hatası (4xx) alan siparişlerin sessizce silinmesini engellemek ve garsona düzeltme olanağı sağlamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-060-waiter-pwa-offline-queue-error-handling.md`
- PO:2026-08-31 kararıyla src/Clients/WaiterPwa/wwwroot/waiter-app.js yüzeyi V1-RMD-066'ya devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-060/**`

## In scope

- `flushOfflineQueue` fonksiyonunda 4xx hata durumunda siparişi silmeyip hata durumu ile işaretlemek ve garsona uyarı göstermek.

## Out of scope

- Backend sipariş modellerini değiştirmek.

## Dependencies

- V1-RMD-059

## Deliverables

- Çevrimdışı sipariş veri kaybını önleyen hata yönetimi.

## Acceptance evidence

- `waiter-app.js` içinde 4xx hatalarında sipariş kuyruktan silinmez.

## Handoff

- V1-RMD-061

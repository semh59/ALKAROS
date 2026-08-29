# V1-RMD-051 - Waiter pwa and cashier ui contract alignment

- Task ID: V1-RMD-051
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

Waiter PWA ve Cashier UI istemcilerini gerçek Host API sözleşmelerine (`/table-management/tables`, `/catalog-management/categories`, `/catalog-management/products`), session/cookie mekanizmasına bağlamak; model/property adlarını hizalamak; 4xx kalıcı hataların çevrimdışı kuyruğa alınmasını önlemek ve HTML injection açıklarını güvenli DOM metotlarıyla kapatmak.

## Owned surface

- `plan/v1/remediation/V1-RMD-051-waiter-pwa-and-cashier-ui-contract-alignment.md`
- `src/Clients/WaiterPwa/wwwroot/waiter-app.js`
- `src/Clients/Cashier/wwwroot/**`
- `tests/Clients/WaiterPwa/Frontend/test_waiter_pwa_frontend.py`
- `evidence/V1-RMD-051/**`

## In scope

- `waiter-app.js` içinde API rotalarını, DTO alan adlarını (`tableId`, `tableNumber`, `capacity`, `productName`) ve ürün listeleme isteklerini doğrulamak.
- `sessionToken`/çerezleri isteklerde göndermek; yalnızca ağ kesintilerini çevrimdışı kuyruğa almak, 4xx istemci hatalarını kuyruğa almamak.
- `innerHTML` yerine `textContent` ve güvenli DOM bağlama yöntemlerini kullanarak XSS/HTML injection açıklarını gidermek.
- `cashier-app.js` içindeki sahte katalog yerine dinamik veya geçerli Guid tabanlı ürün ve masa verisi kullanmak.
- `test_waiter_pwa_frontend.py` testini güncel sözleşmeyi sınayacak şekilde güncellemek.

## Out of scope

- PosTerminal React uygulamasını değiştirmek.

## Dependencies

- V1-RMD-050

## Deliverables

- Gerçek API ile uyumlu, güvenli ve doğru hata işleyen Waiter PWA ve Cashier UI.

## Acceptance evidence

- Waiter PWA frontend testleri ve istemci sözleşme kontrolleri exit code 0 verir.

## Handoff

- V1-RMD-052

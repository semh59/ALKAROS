# V1-RMD-038 - Waiter PWA production route and PWA assets

- Task ID: V1-RMD-038
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Goal

Waiter PWA uygulamasını Docker/Host içinde `/waiter/` gibi açık bir production rotasından sunmak; CSP yapılandırmasını, Service Worker kapsamını ve eksik PWA ikonlarını (`icon-192.png`, `icon-512.png`) tamamlamak.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/index.html`
- `src/Clients/WaiterPwa/wwwroot/manifest.json`
- `src/Clients/WaiterPwa/wwwroot/sw.js`
- `src/Clients/WaiterPwa/wwwroot/icon-192.png`
- `src/Clients/WaiterPwa/wwwroot/icon-512.png`
- PO:2026-08-29 kararıyla src/Host/Program.cs yüzeyi V1-RMD-046'ya devredildi; bu historical task closed kalır.
- `./Dockerfile`
- `evidence/V1-RMD-038/**`

## Dependencies

- V1-WTR-008

## Acceptance evidence

- Waiter PWA `/waiter/` rotasından canlı sunulur.
- Manifest ikonları eksiksiz yüklenir.
- Service Worker `/waiter/` kapsamını yönetir.

## Handoff

- V1-RMD-042

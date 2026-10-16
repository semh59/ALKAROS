# V1-RMD-553 - Vanilla kasa ve garson uygulamalarında raporlama

- Task ID: V1-RMD-553
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Düz JavaScript ile yazılmış kasa ve garson uygulamaları da hata ve etkileşim raporlar; ödeme ekranları dahildir.

## Owned surface

- `plan/v1/remediation/V1-RMD-553-vanilla-clients-reporting.md`
- `evidence/V1-RMD-553/**`
- `src/Clients/Cashier/wwwroot/telemetry/**`
- `src/Clients/WaiterPwa/wwwroot/telemetry/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/index.html
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/index.html
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/telemetry-reporting.test.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/cash-session/index.html - yalnız betik etiketi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/customer-accounts/index.html - yalnız betik etiketi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/index.html - yalnız betik etiketi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/sw.js - yalnız çevrimdışı önbellek listesine betik girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Paylaşılan küçük betik: yakalanmamış hata, ağ hatası, düğme etkileşimi; aynı uç noktalar ve kurallar.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-537
- V1-RMD-541

## Acceptance evidence

1. Ödeme ekranındaki 5 düğme için olay kaydı (StaticApps testi).
2. Garson çevrimdışı sipariş kuyruğu (localStorage) telemetri nedeniyle kota hatası vermez.
3. Çıktılar `evidence/V1-RMD-553/` altındadır.

## Handoff

- None

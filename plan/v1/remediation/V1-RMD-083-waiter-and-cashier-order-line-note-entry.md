# V1-RMD-083 - Waiter and cashier order line note entry

- Task ID: V1-RMD-083
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Garson PWA ve Cashier sipariş ekranlarında her sepet kalemine serbest metin özel talimat girme imkanı eklemek. Sunucu tarafı (`/orders/table-draft`) talimatı zaten `notes` olarak kalıcılaştırır; eksik olan istemci giriş alanları ve sipariş yönetiminin dönüş DTO'sunda talimatı yansıtmasıdır.

## Owned surface

- `plan/v1/remediation/V1-RMD-083-waiter-and-cashier-order-line-note-entry.md`
- `src/Clients/WaiterPwa/wwwroot/waiter-app.js`
- `src/Clients/WaiterPwa/wwwroot/index.html`
- `src/Clients/Cashier/wwwroot/**`
- `tests/Host/Experience/Orders/**`
- `evidence/V1-RMD-083/**`

## In scope

- WaiterPwa sipariş penceresinde her sepet kalemi için özel talimat metin girişi; girilen değerin `specialInstructions` olarak gönderilmesi.
- Cashier fiş akışında her kalem için özel talimat metin girişi; `specialInstructions` olarak gönderilmesi ve aynı ürünün farklı talimatlı satırlarının birleştirilmemesi.
- `OrderManagementStore` üzerinden `specialInstructions` değerinin kaydedilip `OrderItemDto` içinde geri döndüğünü doğrulayan xUnit regresyon testi.

## Out of scope

- Yapısal modifier seçici ve fiyat farkı entegrasyonu.
- PosTerminal DualScreen artışlı sipariş girişine not eklemek.

## Dependencies

- V1-RMD-082

## Deliverables

- Kalem bazlı özel talimat girişi olan garson ve kasiyer sipariş ekranları; talimatı yansıtan sipariş yönetimi sözleşmesi.

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js` ve `node --check src/Clients/Cashier/wwwroot/cashier-app.js` sıfır çıkış kodu verir.
- `dotnet test` host order management testleri sıfır hata verir.
- Semih; garson PWA'da bir kaleme "az" yazıp siparişi gönderdiğinde, mutfak ekranında o kalemde "az" talimatının göründüğünü doğrular.

## Handoff

- V1-GOV-045

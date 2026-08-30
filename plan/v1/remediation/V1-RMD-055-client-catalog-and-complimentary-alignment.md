# V1-RMD-055 - Client catalog and complimentary alignment

- Task ID: V1-RMD-055
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

`cashier-app.js` ve `waiter-app.js` istemcilerinin dinamik katalog isteklerini doğru `/api/v1/terminals/{terminalId}/catalog` rotasına bağlamak; ikram (complimentary) satırlarında sunucu tarafında finansal indirim/sıfır fiyat uygulayarak mali tutarlılığı sağlamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-055-client-catalog-and-complimentary-alignment.md`
- `src/Clients/Cashier/wwwroot/**`
- `src/Clients/WaiterPwa/wwwroot/waiter-app.js`
- `evidence/V1-RMD-055/**`

## In scope

- `cashier-app.js` ve `waiter-app.js` içinde katalog çağrısını `/api/v1/terminals/${state.terminalId}/catalog` rotasına yönlendirmek.
- İkram seçilen kalemlerde sunucuya gönderilen sipariş taslağında `unitPrice: 0` veya indirim tutarını doğru yansıtarak UI ile backend arasındaki finansal tutarsızlığı gidermek.

## Out of scope

- C# backend modellerini değiştirmek.

## Dependencies

- V1-RMD-054

## Deliverables

- Dinamik katalog ile senkronize ve ikram tutarını finansal olarak doğru ileten Kasiyer ve Garson arayüzleri.

## Acceptance evidence

- İstemci katalog istekleri ve ikram sipariş akışı test edilir.

## Handoff

- V1-RMD-056

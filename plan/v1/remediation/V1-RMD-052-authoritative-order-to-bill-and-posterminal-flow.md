# V1-RMD-052 - Authoritative order to bill and posterminal flow

- Task ID: V1-RMD-052
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

`Bill.FromOrder` domain invariantlarını kullanarak siparişten adisyona güvenli ve idempotent geçiş sağlamak; rotayı bağımsız `/from-order/{orderId}` olarak yapılandırmak; PosTerminal `App.tsx` içinde `BillingRoute`'u aktif masa/adisyon bağlamına bağlamak; `TableRoute` içinde seçilen zone değiştikçe floor plan'ı dinamik yüklemek ve `freshness` zamanını son başarılı backend yanıtına bağlamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-052-authoritative-order-to-bill-and-posterminal-flow.md`
- PO:2026-08-29 kararıyla src/Host/Experience/Billing/** yüzeyleri V1-RMD-054 ve V1-RMD-057'ye, src/Clients/PosTerminal/src/App.tsx yüzeyi V1-RMD-056'ya devredildi; bu historical task closed kalır.
- `src/Clients/PosTerminal/src/features/tables/**`
- `src/Clients/PosTerminal/src/features/billing/**`
- `evidence/V1-RMD-052/**`

## In scope

- `BillingSplitStore.cs` içinde `Bill.FromOrder` kullanarak iptal edilmiş kalemleri eleyen, ikram kalemlerini koruyan ve mükerrer faturalamayı `GetByOrderIdAsync` ile engelleyen köprüyü kurmak.
- Rota yapısını bağımsız `POST /api/v1/terminals/{terminalId}/billing/bills/from-order/{orderId}` olarak düzenlemek.
- `App.tsx` içinde `BillingRoute`'un sabit 4 kişi yerine dinamik masa/adisyon katılımcılarını kullanmasını sağlamak.
- `TableRoute` içinde zone değiştiğinde floor plan'ın tetiklenmesini ve dinamik `lastSuccessfulSync` ile `freshness` takibini sağlamak.

## Out of scope

- Kasiyer UI veya Garson PWA istemcilerini değiştirmek.

## Dependencies

- V1-RMD-051

## Deliverables

- Eksiksiz Order -> Bill adisyon geçişi ve PosTerminal entegrasyonu.

## Acceptance evidence

- Sipariş üzerinden adisyon oluşturma ve hesap bölme akışının PosTerminal üzerinden uçtan uca çalıştığı kanıtlanır.

## Handoff

- V1-GOV-029

# V1-RMD-048 - Authoritative order to bill bridge flow

- Task ID: V1-RMD-048
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

Masadaki siparişten adisyon (`Order -> Bill`) üretimini yetkili backend endpoint'ine bağlamak; PosTerminal üzerindeki sabit/varsayılan bill kimliği yerine seçilen masa/sipariş üzerinden dinamik adisyon açma ve bölme akışını kurmak.

## Owned surface

- `plan/v1/remediation/V1-RMD-048-authoritative-order-to-bill-bridge-flow.md`
- `src/Host/Experience/Billing/**`
- `src/Clients/PosTerminal/src/features/billing/**`
- `evidence/V1-RMD-048/**`

## In scope

- Masa siparişinden adisyon üreten yetkili Host/Billing endpoint'i sağlamak.
- PosTerminal `BillingRoute` bileşenini gerçek adisyon kimliği ve masa katılımcılarıyla dinamik olarak çalıştırmak.

## Out of scope

- Gerçek ödeme ve mali fiş işlemlerini uygulamak (V1 dışı).

## Dependencies

- V1-RMD-047

## Deliverables

- Siparişten adisyona kesintisiz geçiş ve dinamik hesap paylaştırma akışı.

## Acceptance evidence

- Masadaki açık siparişten adisyon oluşturulup kişi/ürün bazlı bölme işlemi doğrulanır.

## Handoff

- V1-GOV-027

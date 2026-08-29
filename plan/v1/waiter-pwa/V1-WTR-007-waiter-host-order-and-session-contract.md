# V1-WTR-007 - Waiter host order and session contract

- Task ID: V1-WTR-007
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Goal

Garson mobil istemcisinin gerçek Host API üzerinden oturum doğrulaması yapabilmesi ve masa siparişlerini (`/api/v1/terminals/{terminalId}/orders` veya ilgili table experience rotası üzerinden) idempotent olarak iletebilmesi için Host sözleşmesini netleştirmek ve eksik uç noktaları tamamlamak.

## Owned surface

- `src/Host/Experience/Orders/**`
- `tests/Host/Experience/Orders/**`
- `evidence/V1-WTR-007/**`

## Dependencies

- V1-RMD-037

## Acceptance evidence

- Garson sipariş girişi için Host tarafında authoritative REST endpoint mevcuttur.
- Idempotency anahtarı ve yetki kontrolü doğrulanır.

## Handoff

- V1-WTR-008

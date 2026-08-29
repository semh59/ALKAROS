# V1-RMD-040 - Authoritative order to bill bridge

- Task ID: V1-RMD-040
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Goal

Gönderilmiş (submitted) bir masa siparişinden, ödeme yapmadan ve mali kayıt üretmeden yetkili (authoritative) bir adisyon/bill oluşturan veya mevcut adisyonu getiren Host endpoint ve uygulama köprüsünü eklemek.

## Owned surface

- `plan/v1/remediation/V1-RMD-040-authoritative-order-to-bill-bridge.md`
- PO:2026-08-29 kararıyla src/Host/Experience/Billing yüzeyi V1-RMD-048'e devredildi; bu historical task closed kalır.
- `tests/Host/Experience/Billing/**`
- `evidence/V1-RMD-040/**`

## Dependencies

- V1-RMD-037

## Acceptance evidence

- Masa siparişinden authoritative adisyon/bill üretimi Host API üzerinden doğrulanır.
- Tekrarlı istekler idempotent çalışır.

## Handoff

- V1-RMD-041

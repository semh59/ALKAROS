# V1-RMD-040 - Authoritative order to bill bridge

- Task ID: V1-RMD-040
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Gönderilmiş (submitted) bir masa siparişinden, ödeme yapmadan ve mali kayıt üretmeden yetkili (authoritative) bir adisyon/bill oluşturan veya mevcut adisyonu getiren Host endpoint ve uygulama köprüsünü eklemek.

## Owned surface

- `src/Host/Experience/Billing/**`
- `tests/Host/Experience/Billing/**`
- `evidence/V1-RMD-040/**`

## Dependencies

- V1-RMD-037

## Acceptance evidence

- Masa siparişinden authoritative adisyon/bill üretimi Host API üzerinden doğrulanır.
- Tekrarlı istekler idempotent çalışır.

## Handoff

- V1-RMD-041

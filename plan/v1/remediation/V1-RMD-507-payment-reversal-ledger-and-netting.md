# V1-RMD-507 - Ödeme iade defteri (payment_reversals) ve net ödeme hesabı

- Task ID: V1-RMD-507
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

İade akışının veri tabanı ve alan temeli: değişmez `payments.payment_reversals` defteri, `Payment` için `Refunded` / `PartiallyRefunded` geçişleri, bunları engelleyen DB kısıtının gevşetilmesi ve net ödeme (ödenen − iade) hesabının tahsilat, hesap kapanışı ve raporlarda tutarlı olması. Kapsam taslaktır; başlamadan önce Owned surface keşif bulgularıyla netleştirilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-507-payment-reversal-ledger-and-netting.md`
- `evidence/V1-RMD-507/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `payment_reversals` tablosu, `Payment` iade geçişleri, net ödeme hesabı (allocations toplamı, hesap kapanış hesaplayıcısı, gün sonu ve ödeme dağılımı raporları).

## Out of scope

- İade isteği/onayı ve uçlar (V1-RMD-508); arayüz (V1-RMD-509).

## Dependencies

- V1-RMD-506

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-507/` altındadır.

## Handoff

- None

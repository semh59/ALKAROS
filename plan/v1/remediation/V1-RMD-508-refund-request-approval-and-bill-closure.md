# V1-RMD-508 - İade isteği, onayı ve iade sonrası hesabın kapanması

- Task ID: V1-RMD-508
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

PO kararı (2026-10-13): ödenmiş hesap doğrudan iptal edilmez, önce iade edilir. Nakit iade: kasiyer gerekçeyle ister, müdür onaylar, tutar iade eden kasiyerin açık kasa oturumunda iade olarak görünür. Kart iadesi şimdilik elle: sistem "kart iadesi gerekli" kaydı açar, para banka/POS panelinden elle iade edilir, sonuç (fiş numarası dahil) girilir. Hesabın tüm ödemeleri iade edilince hesap iptal olur. Kapsam taslaktır; başlamadan önce Owned surface keşifle netleştirilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-508-refund-request-approval-and-bill-closure.md`
- `evidence/V1-RMD-508/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Not: V1-RMD-507 yalnız tam iade servisini hazırlar; bu görev ödemeyi `PostgresPaymentReversalRepository` ile iade eder.
- `payments.refund.request` (kasiyer) ve `payments.refund.approve` (süpervizör, müdür) izinleri; iade isteği tablosu ve uçları; nakit iade defteri satırı; kart elle iade kaydı; hesap Paid → Reopened → Cancelled; `payment.refunded` denetim olayı.

## Out of scope

- Arayüz (V1-RMD-509); kısmi iade; banka/POS entegrasyonuyla otomatik kart iadesi.

## Dependencies

- V1-RMD-507

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-508/` altındadır.

## Handoff

- None

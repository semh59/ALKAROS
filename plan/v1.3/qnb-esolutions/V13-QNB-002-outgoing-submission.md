# V13-QNB-002 - Implement QNB outgoing invoice submission

- Task ID: V13-QNB-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.17
- PDF:II.5.11
- PDF:III.20

## Goal

Değişmez bir invoice draft'yi anında gönderin ve provider referanslarını/status geçmişini sürdürün.

## Owned surface

- `src/Modules/Invoicing/Qnb/Outgoing/**`, `tests/Modules/Invoicing/Qnb/Outgoing/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Yük eşleme, idempotency anahtarı, kabul edilen/reddedilen yanıt, status sorgusu ve arındırılmış ham kanıtlar.

## Out of scope

- Gelen alım ve müşteri bakiyesi hesaplaması.

## Dependencies

- V13-QNB-001
- V13-INV-002
- V13-INV-003
- V0-QNB-001
- V1-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/Invoicing/Qnb/Outgoing/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Aynı invoice retry, bir provider belgesi oluşturur; yerel referans sandbox kanıtlarıyla eşleşiyor; reddedilen sonuç
  korunur.

## Handoff

- V13-QNB-004

# V13-INV-002 - Implement outgoing invoice generation

- Task ID: V13-INV-002
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

Onaylanmış GIB/QNB profili altında seçilen kaynak kümesinden invoice başlığını ve vergi gruplu satırları oluşturun.

## Owned surface

- `src/Modules/Invoicing/Generation/**`, `tests/Modules/Invoicing/Generation/**`,
  `database/migrations/V13/V13-INV-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- EFatura/EArşiv seçim girişi, UBL-gerekli tanımlayıcılar, vergi/yuvarlama, değişmez draft ve müşteri anlık görüntüsü.

## Out of scope

- QNB taşıma ve kayıtlı kullanıcı araması.

## Dependencies

- V13-INV-001
- V13-CST-001
- V0-CMP-001
- V0-CMP-002

## Deliverables

- `src/Modules/Invoicing/Generation/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Oluşturulan toplamlar, kaynak işlemler ve vergi gruplarıyla mutabakata varır; nesil hesap bakiyesine ikinci bir borç
  eklemez.

## Handoff

- V13-INV-003
- V13-QNB-002

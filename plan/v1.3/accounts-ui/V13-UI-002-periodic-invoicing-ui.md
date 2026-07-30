# V13-UI-002 - Implement periodic invoicing UI

- Task ID: V13-UI-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.31-I.32

## Goal

Kaynak önizlemesini, kayıtlı kullanıcı sonucunu, draft incelemesini, gönderme/status ve workflow iptalini uygulayın.

## Owned surface

- `src/Clients/Cashier/Invoicing/**`, `tests/Clients/Cashier/Invoicing/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Dönem kilidi, kaynak/satır takibi, vergi toplamları, QNB durumları, retry-güvenli eylemler ve mutabakat bağlantıları.

## Out of scope

- Gelen tedarikçi invoice ve genel muhasebe muhasebesi.

## Dependencies

- V13-INV-001
- V13-INV-002
- V13-INV-003
- V13-INV-004
- V13-QNB-001
- V13-QNB-002
- V13-QNB-005
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/Invoicing/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Kullanıcı PendingProvider'yi körü körüne yeniden gönderemez; kaynaklara kadar izlenen toplamlar; iptal belirsizliği
  görünür ve çözümsüz kalıyor.
- `V13-QNB-005` kanıtlı `NotApplicable` ise provider cancellation action etkinleşmez; UI aynı tarihli capability
  kararına bağlı typed unavailable sonucu gösterir.
- Invoicing UI, `V0-CMP-005` kararındaki operations UI success criteria ve exception kayıtlarını karşılar.

## Handoff

- None

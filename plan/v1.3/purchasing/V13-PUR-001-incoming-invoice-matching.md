# V13-PUR-001 - Implement supplier account and incoming-invoice matching

- Task ID: V13-PUR-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:III.15

## Goal

Envanteri iki kez değiştirmeden, gelen invoice satırlarını tedarikçi, satın alma makbuzu ve borç hesabı girişleriyle
eşleştirin.

## Owned surface

- `src/Modules/Purchasing/InvoiceMatching/**`, `tests/Modules/Purchasing/InvoiceMatching/**`,
  `database/migrations/V13/V13-PUR-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Tedarikçi hesap defteri, satır eşleşme toleransları, makbuz bağlantıları, maliyet güncelleme kaynağı ve uyumsuzluk
  durumu.

## Out of scope

- QNB alma ve satın alma-order makbuz gönderimi.

## Dependencies

- V11-PUR-001
- V11-PUR-002
- V11-RCP-002
- V13-QNB-003

## Deliverables

- `src/Modules/Purchasing/InvoiceMatching/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Eşleşen invoice, tek bir ödenebilir giriş oluşturur ve yinelenen stok hareketi olmaz; miktar/fiyat uyumsuzluğu
  mutabakatı açar.

## Handoff

- V15-REC-001

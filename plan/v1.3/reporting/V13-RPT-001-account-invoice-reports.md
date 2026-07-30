# V13-RPT-001 - Implement customer account and invoice reports

- Task ID: V13-RPT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.20
- PDF:II.10
- PDF:III.31

## Goal

Hesap yaşlandırma, invoice yaşlandırma/status, gelen eşleşme ve tedarikçi borç raporlarını uygulayın.

## Owned surface

- `src/Modules/Reporting/AccountsInvoicing/**`, `tests/Modules/Reporting/AccountsInvoicing/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Tarih itibarıyla, dönem, müşteri/tedarikçi, verilen/iptal edilen/mutabakat durumları ve defter toplamları.

## Out of scope

- Operasyonel kontrol paneli UI ve çevrimiçi kanal ölçümleri.

## Dependencies

- V0-DOM-008
- V13-ACC-002
- V13-INV-003
- V13-PUR-001

## Deliverables

- `src/Modules/Reporting/AccountsInvoicing/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Yaşlanan toplamlar, değişmez defterlerle uzlaştırılır ve iptal edilen invoice'yi tam olarak V0-DOM-007'ye göre hariç
  tutar/yeniden sınıflandırır.

## Handoff

- V15-RPT-001

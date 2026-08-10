# V11-RPT-001 - Implement menu production and inventory reports

- Task ID: V11-RPT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.20
- PDF:II.10
- PDF:III.31

## Goal

Satış oranı, porsiyon tüketimi, production, atık ve kritik stok raporlarını uygulayın.

## Owned surface

- `src/Modules/Reporting/MenuInventory/**`, `tests/Modules/Reporting/MenuInventory/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Metrik sözleşmeler, hizmet günü filtreleri, konum, tarif sürümü ve kaynak mutabakatı.

## Out of scope

- Finansal satışlar, tedarikçiye ödenecek tutarlar ve gösterge tablosu stili.

## Dependencies

- V0-DOM-008
- V11-MNU-002
- V11-PRD-002
- V11-INV-002
- V11-INV-005
- V11-INV-006

## Deliverables

- `src/Modules/Reporting/MenuInventory/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Rapor toplamları hareket/projeksiyon yeniden yapılandırmalarıyla uzlaştırılır; hiçbir rapor yansıtmayı gerçeğin ikinci
  kaynağı olarak ele almıyor.

## Handoff

- V15-RPT-001

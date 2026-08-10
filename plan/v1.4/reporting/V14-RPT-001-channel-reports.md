# V14-RPT-001 - Implement channel reports

- Task ID: V14-RPT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.20
- PDF:II.10
- PDF:III.31

## Goal

Onaylanan metrik tanımlarından QR ve çevrimiçi kanal hacim, değer, iptal ve mutabakat metriklerini raporlayın.

## Owned surface

- `src/Modules/Reporting/Channels/**`, `tests/Modules/Reporting/Channels/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kaynak/kanal boyutları, iş tarihi filtreleri, order değeri, ret/iptal sayıları ve mutabakat farkı.

## Out of scope

- Metrik tanımı değişiklikleri, operasyonel komut yönetimi ve birleştirilmiş alanlar arası kontrol paneli.

## Dependencies

- V0-DOM-008
- V14-REC-001
- V14-ONL-005

## Deliverables

- Sürümlendirilmiş kanal raporu sorguları/API.
- İptalleri, yeniden denemeleri, saat dilimlerini ve yinelenen webhook'larnı kapsayan altın veri kümesi testleri.

## Acceptance evidence

- Rapor toplamları, aynı iş tarihi aralığı için onaylanmış order ve mutabakat kaynağı kayıtlarıyla mutabakat sağlar.
- `V14-REC-001` kanıtlı `NotApplicable` ise kanal raporları onaylanmış order kaynaklarıyla yine mutabakat sağlar;
  mutabakat vaka kaynağı beklenmez.

## Handoff

- V15-RPT-001

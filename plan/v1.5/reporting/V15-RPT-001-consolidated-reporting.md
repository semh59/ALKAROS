# V15-RPT-001 - Implement consolidated reporting

- Task ID: V15-RPT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.20
- PDF:II.10
- PDF:III.31

## Goal

Onaylanmış operasyonel, stok, payment, invoice ve kanal rapor sözleşmeleri üzerinde mutabakata varılmış bir raporlama
giriş noktası gösterin.

## Owned surface

- `src/Modules/Reporting/Consolidated/**`, `tests/Modules/Reporting/Consolidated/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Rapor kataloğu, paylaşılan iş tarihi filtreleri, yetkilendirme, dışa aktarma limitleri ve çapraz rapor detaylandırma
  tanımlayıcıları.

## Out of scope

- Kaynak ölçümlerini yeniden tanımlama, finansal işlemleri yazma ve özel SQL erişimi.

## Dependencies

- V1-RPT-001
- V11-RPT-001
- V12-RPT-001
- V13-RPT-001
- V14-RPT-001
- V15-REC-001

## Deliverables

- Sürümlendirilmiş birleştirilmiş raporlama API/interface.
- Rol, dışa aktarma sınırı, saat dilimi ve çapraz rapor tutarlılık testleri.

## Acceptance evidence

- Aynı onaylı filtre, izlenebilir kaynak tanımlayıcıları ve özet ve ayrıntılı raporlarda tutarlı toplamlar sağlar.
- `V12-RPT-001` kanıtlı `NotApplicable` ise meal-card settlement bölümü raporlarda disabled olarak gösterilir; kalan
  rapor sözleşmeleri aynı acceptance ile yine doğrulanır.
- `V15-REC-001` kanıtlı `NotApplicable` ise reconciliation vaka toplamları birleşik rapora dahil edilmez; kalan rapor
  sözleşmeleri aynı acceptance ile yine doğrulanır.
- `V14-RPT-001` kanıtlı `NotApplicable` ise kanal raporu kaynakları birleşik rapora dahil edilmez; kalan rapor
  sözleşmeleri aynı acceptance ile yine doğrulanır.

## Handoff

- V20-UAT-002
- V20-GAT-002

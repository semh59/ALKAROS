# V15-SUP-001 - Implement redacted diagnostic bundle

- Task ID: V15-SUP-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.38
- PDF:I.42

## Goal

Gizli bilgileri, payment verilerini veya gereksiz kişisel verileri dışarı aktarmadan olayları teşhis eden sınırlı bir
destek paketi oluşturun.

## Owned surface

- `src/Modules/Support/DiagnosticBundle/**`, `tests/Modules/Support/DiagnosticBundle/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Sistem durumu özeti, sürüm/yapılandırma parmak izleri, seçilen korelasyon günlükleri, redaksiyon, boyut/zaman
  sınırları ve paket denetimi.

## Out of scope

- Uzaktan kabuk erişimi, veritabanı dökümleri, otomatik harici yükleme ve olay çözümü.

## Dependencies

- V15-OBS-001
- V15-SEC-003
- V0-CMP-003

## Deliverables

- Yetkili tanılama paketi komutu/arabirimi.
- Gizli/PII sızıntısı, boyut sınırı, zaman penceresi ve eşzamanlı üretim testleri.

## Acceptance evidence

- Otomatik tohumlanmış gizli tarama, pakette korunan bir değer bulamaz; Paket menşei ve istekte bulunan aktör
  denetlenebilir olmaya devam eder.

## Handoff

- V20-DOC-002
- V20-GAT-002

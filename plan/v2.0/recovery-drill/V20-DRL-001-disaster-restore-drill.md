# V20-DRL-001 - Execute disaster restore drill

- Task ID: V20-DRL-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.45-I.54
- PDF:II.2.23
- PDF:III.25

## Goal

release adayını onaylanmış tesis dışı yedeklemeden yalıtılmış, temiz bir ortama geri yükleyin ve kurtarma hedeflerini
ölçün.

## Owned surface

- `release/evidence/recovery/**`, `tools/release/restore-drill/**`
- Bu görev backup/restore ürün kodunu değiştiremez.

## In scope

- Yedekleme seçimi, anahtar erişimi, temiz geri yükleme, hizmet önyüklemesi, bütünlük kontrolleri, mutabakat ve RPO/RTO
  ölçümü.

## Out of scope

- Kurtarma hedeflerini değiştirme, production yük devretme ve yedekleme kusurlarını düzeltme.

## Dependencies

- V0-BKP-002
- V15-BKP-001
- V15-BKP-002
- V20-INS-001

## Deliverables

- Zaman damgalı tatbikat metni, bütünlük raporu ve ölçülen RPO/RTO.

## Acceptance evidence

- Temiz ortam geri yüklemesi, onaylanan RTO dahilinde tamamlanır, veri kaybı, onaylanan RPO dahilinde kalır ve gerekli
  tüm bütünlük/mutabakat kontrolleri geçer.

## Handoff

- V20-MIG-002
- V20-GAT-002

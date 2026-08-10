# V20-MIG-002 - Rehearse migration rollback

- Task ID: V20-MIG-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.45-I.54
- PDF:II.13-II.15
- PDF:III.29-III.40
- CORR:C1

## Goal

Taşınan release adayından migration öncesi kurtarılabilir durumuna onaylanmış geri alma yolunu kanıtlayın.

## Owned surface

- `release/evidence/migration/rollback/**`, `tools/release/rollback-rehearsal/**`
- Bu görev ürün migration veya backup uygulama kodunu değiştiremez.

## In scope

- Tetikleme noktasını geri alma, yazma dondurma, migration'yi ters çevirme veya kararı geri yükleme, yürütme zamanlaması
  ve bütünlük karşılaştırması.

## Out of scope

- RPO/RTO, production geri almanın tanımlanması ve başarısız geçişlerin düzeltilmesi.

## Dependencies

- V20-MIG-001
- V20-DRL-001

## Deliverables

- Tekrarlanabilir geri alma provası ve karar kaydı.
- Geri yüklenen durum bütünlüğü ve mutabakat raporu.

## Acceptance evidence

- Prova edilen yol, sistemi tüm kontrol toplamları açıklanarak RTO dahilinde ve onaylanmış RPO kayıp sınırı dahilinde
  onaylanmış checkpoint'ye döndürür.
- `V20-MIG-001` kanıtlı `NotApplicable` ise ileri yön provası kapsam dışı kalır; geri alma provası kendi kaynaklarıyla
  RTO/RPO ve kontrol toplamı kuralına yine uyar.

## Handoff

- V20-GAT-002
- V20-REL-003

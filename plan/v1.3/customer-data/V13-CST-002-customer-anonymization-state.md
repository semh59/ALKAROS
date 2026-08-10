# V13-CST-002 - Implement customer anonymization state transitions

- Task ID: V13-CST-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.15
- PDF:II.3.11
- PDF:III.18

## Goal

Legal olarak korunan financial reference'ları silmeden Requested, RetentionBlocked, Pending ve Anonymized durumlarını
uygulamak.

## Owned surface

- `src/Modules/CustomerData/AnonymizationState/**`, `tests/Modules/CustomerData/AnonymizationState/**`,
  `database/migrations/V13/V13-CST-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Durum makinesi, saklama kontrolü, bağımsız alan değiştirme ve denetim event.

## Out of scope

- Modüller arası yük temizleme ve planlı saklama yürütme.

## Dependencies

- V13-CST-001
- V1-OPS-001
- V0-DOM-001

## Deliverables

- `src/Modules/CustomerData/AnonymizationState/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tekrarlanan anonimleştirme stabildir; alıkoyma-blocked isteği PII'yi korur ve nedenini kaydeder; izin verilen istek
  yalnızca yapılandırılmış alanları kaldırır.

## Handoff

- V15-KVK-001
- V15-KVK-002

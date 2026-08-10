# V14-MAP-001 - Implement provider product mapping

- Task ID: V14-MAP-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

provider ürün/değiştirici tanımlayıcılarını, açık eşlenmemiş davranışa sahip etkin dahili katalog öğeleriyle eşleyin.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/**`,
  `tests/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/**`, `database/migrations/V14/V14-MAP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Eşleme benzersizliği, etkin tarihler, değiştirici doğrulama ve eşlenmemiş reddetme.

## Out of scope

- Katalog dışa aktarma/güncelleme ve status senkronizasyonu.

## Dependencies

- V1-CAT-001
- V0-YSP-001

## Deliverables

- `src/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bir dış ürün, bir aktif iç ürüne çözümlenir; eksik/belirsiz eşleme Order oluşturamaz.

## Handoff

- V14-ONL-002

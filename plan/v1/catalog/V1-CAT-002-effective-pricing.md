# V1-CAT-002 - Implement effective-dated product pricing

- Task ID: V1-CAT-002
- Status: Done
- Assignee: opencode-v1-cat-002
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:II.2.2
- PDF:III.4

## Goal

Bağımsız yazılabilir duplicate price state oluşturmadan price record'larını ve authoritative effective-price query'yi
uygulamak.

## Owned surface

- `src/Modules/Catalog/Pricing/**`, `tests/Modules/Catalog/Pricing/**`, `database/migrations/V1/V1-CAT-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Çakışmayan etkili dönemler, fiyat türü, para birimi ve deterministik zaman içinde arama.

## Out of scope

- Promosyonlar, indirimler ve günlük menüyü geçersiz kılan fiyatlandırma.

## Dependencies

- V1-CAT-001
- V0-CMP-002
- V0-DAT-004

## Deliverables

- `src/Modules/Catalog/Pricing/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Çakışan etkin aralıklar reddedilir ve herhangi bir zaman damgası, ürün/tür/para birimi başına en fazla bir fiyata
  çözümlenir.

## Handoff

- V1-ORD-001
- V11-MNU-001

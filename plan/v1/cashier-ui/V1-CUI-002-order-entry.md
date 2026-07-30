# V1-CUI-002 - Implement cashier Order entry

- Task ID: V1-CUI-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.8

## Goal

Product/modifier seçimi, note, Draft düzenleme ve idempotent submit akışını Türkçe UI ile uygulamak.

## Owned surface

- `src/Clients/Cashier/OrderEntry/**`, `tests/Clients/Cashier/OrderEntry/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Katalog arama, değiştiriciler, draft toplamları, anahtar gönderme ve etki alanı hata eşlemesi.

## Out of scope

- Payment, bill ve production/envanter ekranlarını ayırın.

## Dependencies

- V1-CUI-001
- V1-ORD-001
- V1-ORD-002
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/OrderEntry/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Çift tıklama/retry bir Order oluşturur; geçersiz değiştirici/fiyat değişikliği sunucu sonucunu gösterir ve
  kurtarılabilir draft'yi korur.
- Order entry, `V0-CMP-005` kararındaki cashier success criteria ve approved exception kayıtlarını karşılar.

## Handoff

- V1-CUI-003

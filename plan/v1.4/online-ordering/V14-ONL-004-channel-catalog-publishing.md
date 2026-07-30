# V14-ONL-004 - Publish channel catalog

- Task ID: V14-ONL-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Onaylanan menü/ürün projeksiyonunu, deterministik harici tanımlayıcılarla etkinleştirilmiş her çevrimiçi-order kanalına
yayınlayın.

## Owned surface

- `src/Modules/OnlineOrdering/CatalogPublishing/**`, `tests/Modules/OnlineOrdering/CatalogPublishing/**`,
  `database/migrations/V14/V14-ONL-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Provider yeteneği contract, katalog projeksiyonu, bağımsız yayınlama, harici ID kalıcılığı, retry ve sonuç denetimi.

## Out of scope

- Stok kullanılabilirliği, fiyat sahipliği, gelen order webhook'lar ve operatör UI.

## Dependencies

- V14-MAP-001
- V11-MNU-002
- V0-YSP-001

## Deliverables

- Onaylanan her kanal için Provider'ye özel katalog yayıncısı.
- etkin provider'lar için Contract testleri ve gerçek sandbox kanıtları.
- Açık doğrulama hataları olarak kaydedilen desteklenmeyen provider yetenekleri.

## Acceptance evidence

- Aynı yayınlamanın tekrarlanması harici ürünün kopyalanmasına neden olmaz; eşlenen adlar, fiyatlar, vergi meta verileri
  ve değiştirici yapı, onaylanan provider yanıtıyla eşleşir.

## Handoff

- V14-ONL-005
- V14-OUI-001

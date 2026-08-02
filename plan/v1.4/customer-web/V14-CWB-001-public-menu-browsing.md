# V14-CWB-001 - Build public menu browsing

- Task ID: V14-CWB-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21

## Goal

Authenticated QR customer session için available sellable menu'yü internal management verisini açmadan sunmak.

## Owned surface

- `src/Apps/CustomerWeb/Menu/**`, `tests/Apps/CustomerWeb/Menu/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kategori navigasyonu, ürün ayrıntıları, fiyat/alerjen/bulunabilirlik sunumu ve eski oturum yönetimi.

## Out of scope

- Sepet gönderimi, payment, QR token verilmesi ve menü yönetimi.

## Dependencies

- V14-QRS-003
- V11-MNU-003
- V14-STK-001
- V0-CMP-005

## Deliverables

- Duyarlı genel menü arayüzü.
- Erişilebilirlik, yetkilendirme, eski veriler ve kullanılamayan öğe testleri.

## Acceptance evidence

- Geçerli bir table oturumu yalnızca yayınlanmış satılabilir öğeleri görür; iptal edilen/süresi dolan oturumlar ve
  kullanılamayan ürünler, dahili tanımlayıcılar sızdırılmadan işlenir.
- Public menu, `docs/compliance/accessibility-target.md`'deki customer QR kriterleri ve device/browser matrix'ini
  karşılar.

## Handoff

- V14-CWB-002

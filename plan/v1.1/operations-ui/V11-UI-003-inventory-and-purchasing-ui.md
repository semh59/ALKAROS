# V11-UI-003 - Implement inventory and purchasing operations UI

- Task ID: V11-UI-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.23-I.25

## Goal

İzin verilen gerekçelerle stok bakiyesi, satın alma fişi, ayarlama ve atık ekranlarını uygulayın.

## Owned surface

- `src/Clients/Cashier/InventoryPurchasing/**`, `tests/Clients/Cashier/InventoryPurchasing/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Lokasyona, kısmi makbuz, düzeltme, israf, eşzamanlılık hatası ve denetim nedenine göre bakiyeler.

## Out of scope

- Tedarikçiye ödenecek/gelecek invoice ve raporlama kontrol paneli.

## Dependencies

- V11-PUR-001
- V11-PUR-002
- V11-INV-002
- V11-INV-003
- V11-INV-007
- V11-INV-004
- V11-INV-005
- V11-INV-006
- V0-CMP-005
- V11-UI-002

## Deliverables

- `src/Clients/Cashier/InventoryPurchasing/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- UI asla bakiyeyi doğrudan yazmaz; tekrarlanan alım/ayarlama eyleminin tek bir sunucu etkisi vardır; eski satır
  reddedildi.
- Inventory/purchasing ekranları `V0-CMP-005` kararındaki operations UI success criteria listesini karşılar.

## Handoff

- None

# V12-PUI-002 - Implement cashier CashSession UI

- Task ID: V12-PUI-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.44

## Goal

Aktif terminal/kasiyer için açma, sayma, kapatma ve fark teyit akışını uygulayın.

## Owned surface

- `src/Clients/Cashier/Payments/CashSession/**`, `tests/Clients/Cashier/Payments/CashSession/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Açılış bakiyesi, cash giriş/çıkış, sayım, beklenen/gerçek fark, izin ve eski sürüm yönetimi.

## Out of scope

- Banka/yemek kartı payment ekranları ve mutabakat kontrol paneli.

## Dependencies

- V12-CSH-001
- V12-CSH-002
- V1-CSH-001
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/Payments/CashSession/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- İkinci açık oturum engellenir; kapatma sayımı gerektirir; farkın üzerine yazılamaz ve denetlenmiş olarak kalır.
- Cash session ekranı `V0-CMP-005` kararındaki cashier success criteria listesini karşılar.

## Handoff

- V12-PUI-003

# V1-CUI-001 - Implement cashier shell and table view

- Task ID: V1-CUI-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.7
- PDF:I.9-I.10

## Goal

Türkçe cashier shell, authenticated session ve concurrency-aware Table status görünümünü uygulamak.

## Owned surface

- `src/Clients/Cashier/TableShell/**`, `tests/Clients/Cashier/TableShell/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Oturum açma/oturum, table bölgeleri/status, eski yenileme ve net hata sunumu.

## Out of scope

- Order girişi, bill payment ve ayar yönetimi.

## Dependencies

- V1-IAM-003
- V1-TBL-001
- V1-TBL-005
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/TableShell/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Süresi dolan oturum oturum açmaya geri döner; table güncellemeleri satır-sürüm çakışmalarını yansıtır; hiçbir UI-only
  kilidi yetkili olarak değerlendirilmez.
- Cashier shell, `V0-CMP-005` kararındaki cashier success criteria ve device/browser matrix'ini karşılar.

## Handoff

- V1-CUI-002

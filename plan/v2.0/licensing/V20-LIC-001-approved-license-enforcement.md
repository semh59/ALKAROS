# V20-LIC-001 - Implement approved license enforcement

- Task ID: V20-LIC-001
- Status: Blocked
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.24
- PDF:III.26

## Goal

Yalnız V0-LIC-001 sonucu Required ise onaylanan license enforcement davranışını uygulamak.

## Owned surface

- `src/Modules/Licensing/**`, `tests/Modules/Licensing/**`, `database/migrations/V20/V20-LIC-001/**`
- Bu görev lisans iş kuralını yeniden tanımlayamaz.

## In scope

- Signed license validation, scope/expiry, clock-tamper policy, offline grace ve auditable enforcement.

## Out of scope

- License server uydurma, remote kill switch ve unapproved telemetry.

## Dependencies

- V0-LIC-001
- V15-SEC-001
- V1-SET-001

## Blocker

- V0-LIC-001 henüz Required veya NotApplicable kararı üretmemiştir. Required ise bu task `Planned`, NotApplicable ise
  gerçek assignee ve tarihli karar kanıtıyla `NotApplicable` yapılır.
- Task dosyası ve dependency kimliği her iki sonuçta korunur; blocker ancak tarihli V0 kararıyla kaldırılabilir.

## Deliverables

- Onaylanan enforcement production code'u, failure reason code'ları ve recovery testleri.
- NotApplicable ise yalnız V0 karar kimliği, tarih, approver ve artifact üretilmediği kanıtı.

## Acceptance evidence

- Davranış V0 contract case'leriyle eşleşir; network loss, expiry ve clock anomaly Order, Payment veya fiscal kayıtları
  sessizce bozamaz.

## Handoff

- V20-LIC-002
- V20-GAT-001

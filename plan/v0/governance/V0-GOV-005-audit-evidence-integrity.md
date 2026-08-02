# V0-GOV-005 - Reconcile audit evidence with current task state

- Task ID: V0-GOV-005
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

V0 gate kapanis kaydi, audit manifesti ve audit raporunu guncel task
durumlariyla uyumlu hale getirmek; eski sayim veya kapanis iddiasini kanit
olarak kullanmamak.

## Owned surface

- `evidence/v0/gate-v0-exit-closure.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `plan/VALIDATION_CONTRACT.md`
- `plan/TRACEABILITY.md`
- `evidence/V0-GOV-005/**`

## In scope

- V0 task durumlarini mekanik olarak yeniden saymak ve gate'in acik oldugunu
  dogrulanabilir kayitta gostermek.
- Aktif Markdown envanterini ve hash manifestini yeniden uretmek.
- Eski closure kaydini tarihsel artifact olarak saklayip karar kaniti
  olmadigini acikca belirtmek.

## Out of scope

- V0 Blocked task'i Done yapmak, business/legal/provider karari secmek,
  application code veya migration degistirmek.

## Dependencies

- V0-GOV-003
- V0-GOV-004

## Deliverables

- Guncel V0 gate durumu, yeniden uretilmis audit manifest/raporu ve komut
  transcript'i.

## Acceptance evidence

- Gate kaydindaki task sayilari plan metadata ile birebir eslesir.
- AUDIT_MANIFEST aktif Markdown yolunun tamamini kapsar ve verify-manifest
  exit code 0 verir.
- V0 gate kapanis iddiasi acik Blocked task varken yer almaz ve reddedilir.

## Handoff

- GATE-V0-EXIT

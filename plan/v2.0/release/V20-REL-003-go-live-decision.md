# V20-REL-003 - Record go-live decision

- Task ID: V20-REL-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: release gate
- Surface state: Planned

## Source basis

- PDF:I.45-I.54

## Goal

Exact immutable release candidate için evidence-backed approve veya reject kararını kaydetmek.

## Owned surface

- `release/decisions/go-live/**`
- Bu görev artifact, test sonucu veya kaynak kanıtı değiştiremez.

## In scope

- Gate completeness, approver identity, artifact hash, deployment window, rollback trigger/owner ve explicit
  approve/reject sonucu.

## Out of scope

- Failed gate waiver, product fix ve production deployment execution.

## Dependencies

- V20-GAT-002
- V20-CMP-001
- V20-SEC-001

## Deliverables

- Exact artifact hash'lerine bağlı signed go-live decision record.

## Acceptance evidence

- Bütün mandatory gate'ler geçer ve açık critical/high defect yoksa approve; aksi durumda blocking evidence ile reject
  kaydedilir. Deployment çalıştırılmaz.
- `V20-GAT-002` kanıtlı `NotApplicable` ise evidence pack gate'i go-live kararında beklenmez; kalan mandatory gate'ler
  yine evidence ile doğrulanır.
- `V20-CMP-001` kanıtlı `NotApplicable` ise compliance sign-off gate'i go-live kararında beklenmez; kalan mandatory
  gate'ler yine evidence ile doğrulanır.

## Handoff

- V20-REL-004

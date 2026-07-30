# V0-ARC-007 - Define deployment compatibility matrix

- Task ID: V0-ARC-007
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision
- Surface state: Planned

## Source basis

- PDF:I.45.1
- EXT:DOTNET-SUPPORT-2026-07
- EXT:POSTGRESQL-18.4
- CORR:C15

## Goal

Desteklenen OS, architecture, .NET patch, PostgreSQL patch, package, install ve update sınırlarını belirlemek.

## Owned surface

- `docs/architecture/deployment-compatibility-matrix.md`
- Bu görev başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Minimum/maximum target, prerequisite, privilege, fresh install, update source ve rollback compatibility.

## Out of scope

- Installer code, fleet management ve production deployment.

## Dependencies

- V0-ARC-001

## Deliverables

- Tek decision record: kaynaklar, erişim tarihleri, onaylayan, seçilen sonuç, reddedilen alternatifler ve etkilenen task
  kimlikleri.
- Seçilen supported/unsupported matrix ve source version değerleri.

## Acceptance evidence

- Her installer test target'ı tek destek durumuna ve doğrulanabilir prerequisite listesine sahiptir.

## Handoff

- V20-INS-001
- V20-INS-002

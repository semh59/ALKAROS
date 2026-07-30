# V20-REL-004 - Execute approved production deployment

- Task ID: V20-REL-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: release
- Surface state: Planned

## Source basis

- PDF:I.45-I.54
- PDF:II.13-II.15

## Goal

Yalnız signed Approve kararı verilen exact release artifact'ını kontrollü production deployment ile kurmak.

## Owned surface

- `release/evidence/production-deployment/**`
- Bu görev source, artifact, migration veya go-live kararını değiştiremez.

## In scope

- Artifact hash doğrulama, backup checkpoint, secret provisioning kanıtı, migration, selected QR relay/local connector
  deployment, smoke test ve rollback trigger.

## Out of scope

- Gate waiver, product fix, farklı artifact seçimi ve hypercare gözlemi.

## Dependencies

- V20-REL-003
- V15-BKP-002
- V20-MIG-002
- V20-SEC-001
- V14-QRT-001

## Deliverables

- Exact artifact ve environment kimliğine bağlı immutable deployment transcript'i.
- Backup, migration, smoke, rollback kararı ve yetkili operator kayıtları.

## Acceptance evidence

- `V20-REL-003` sonucu Approve değilse hiçbir production adımı çalışmaz.
- Başarılı kurulum aynı artifact hash'ini, son backup checkpoint'ini ve kontrollü ilk smoke işlemini kanıtlar.
- `V0-ARC-009` selected topology applicable ise public relay'den authenticated local connector'a production smoke
  kanıtlanır; seçilmemiş relay/provider artifact'i dağıtılmaz.

## Handoff

- V20-REL-005

# V1-IAM-002 - Implement role and permission enforcement

- Task ID: V1-IAM-002
- Status: Done
- Assignee: opencode-v1-iam-002
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.1
- PDF:III.3

## Goal

Role, permission, assignment ve server-side authorization check davranışlarını uygulamak.

## Owned surface

- `src/Modules/Identity/Authorization/**`, `tests/Modules/Identity/Authorization/**`,
  `database/migrations/V1/V1-IAM-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- İzin kataloğu, rol atamaları, politika değerlendirmesi ve reddetme denetimi kancası.

## Out of scope

- Kimlik doğrulama ve cihaz oturumu yaşam döngüsü.

## Dependencies

- V1-IAM-001
- V0-DAT-002

## Deliverables

- `src/Modules/Identity/Authorization/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Her korunan komutun adlandırılmış bir izni vardır; reddedilen eylemler alan değişikliği yapmaz; izin testleri izin
  verilen ve reddedilen aktörleri kapsar.

## Handoff

- None

# V1-IAM-008 - Independently verify authorization linearization

- Task ID: V1-IAM-008
- Status: Blocked
- Assignee: opencode-v1-iam-008
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

role/permission revoke commit'inden sonra concurrent authorization sonucunun fail-closed deny olduğunu deterministic
transaction interleaving'iyle doğrulamak.

## Owned surface

- `src/Modules/Identity/Authorization/RoleManagementService.cs`
- `tests/Modules/Identity/Authorization/RoleManagementServiceTests.cs`
- `evidence/V1-IAM-008/**`

## In scope

- `CODE-008` için authorization predicate ve protected write linearization kuralını uygulamak ve formatter-only drift'i
  aynı owned testte kapatmak.

## Out of scope

- Owned surface dışındaki authorization, migration, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-IAM-002

## Blocker

DEPENDENCY_GATES KURALI: `V0-GOV-032` (permission grants auto-expiry) ile `V0-GOV-041` ve
`V0-GOV-042` (dynamic role mapping) `Blocked` kaldığı sürece bu görev `InProgress`
olamaz; `V0-GOV-041`/`V0-GOV-042` 2026-08-13 kullanıcı onaylı plan değişikliği
(C65, `V0-GOV-062`) tarafından defer edilmemiştir, bu devir listesinde yalnız
`V0-REV-001..030` vardır. Blocker ancak kullanıcının sağlayacağı iki kanıtla
kapanır: (1) iki görev için açık `InProgress` thread ID'si taşıyan gerçek
workflow URL/SHA kanıtı ve (2) iki görev kapanış tarih/kararında yazılı ibare
olarak ulaşılabilen, tarihli named repository admin readback (ad-soyad,
kurum/rol, onay tarihi). Bu kanıtlar ilgili görevlerin (GOV-041/GOV-042)
`evidence/` klasörlerine yazılır; bu görev (V1-IAM-008) yalnız `Status`/
`Assignee` metadata blokajını kaldırır, başka yerde kanıt yazmaz.

## Deliverables

- Authorization linearization implementation diff'i, concurrency testleri ve raw transcript.

## Acceptance evidence

- Permission decision ve mutation chosen linearization kuralına göre aynı transaction/conditional write ile bağlanır.
- Authorization tests, `dotnet format ALKAROS.slnx --verify-no-changes --no-restore` ve plan validator exit code `0`
  verir.

## Handoff

- V0-GOV-045

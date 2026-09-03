# V1-IAM-021 - Time Boxed Delegation

- Task ID: V1-IAM-021
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`authorization_delegations` tablosu (grantee, permission_code, limit_amount, expires_at) ile kendiliğinden geri alma işi kurulur; bir yönetici belirli bir saate kadar sınırlı ikram yetkisini devreder, süre dolunca yetki akışı yeniden yöneticiye döner.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-021-time-boxed-delegation.md`
- `database/migrations/V1/V1-IAM-021/**`
- `src/Modules/Identity/Authorization/021/**`
- `tests/Modules/Identity/Authorization/021/**`
- `evidence/V1-IAM-021/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-019

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` sıfır uyarı ile sıfır hata; bu görevin yeni testleriyle ilgili `dotnet test` süzgeci geçer.
- Veri değişiyorsa migration ileri ile geri yönde boş veritabanında denenir.
- Semih için gerçek senaryo: devir oluşturulur; süre içinde yetki kaydı `policy_path=delegation` ile devreden kişiyi taşır; süre dolduğunda aynı istek yeniden yönetici kararına düşer.

## Handoff

- V1-IAM-022

# V1-IAM-021 - Time Boxed Delegation

- Task ID: V1-IAM-021
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`authorization_delegations` (grantee, permission_code, limit_amount, expires_at) + otomatik geri alma isi. Bir yonetici "X 22:00'a kadar <= 200 TL comp" devreder; sure dolunca grant akisi tekrar yoneticiye gider.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-021-time-boxed-delegation.md`
- `database/migrations/V1/V1-IAM-021/**`
- `src/Modules/Identity/Authorization/021/**`
- `tests/Modules/Identity/Authorization/021/**`
- `evidence/V1-IAM-021/**`
- Bu gorev, baska bir task'in owned surface alanini degistiremez.

## Dependencies

- V1-IAM-019

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` 0 uyari / 0 hata; `dotnet test` yetkilendirme filtresi bu gorevin yeni testleriyle gecer.
- Veri degisiyorsa migration ileri/geri bos veritabaninda denenir.
- Semih icin gercek senaryo: devir olusturulur; sure icinde grant `policy_path=delegation` (delegator kayitli); sure dolar -> ayni istek manuel yonetici kararina dusrer.

## Handoff

- V1-IAM-022

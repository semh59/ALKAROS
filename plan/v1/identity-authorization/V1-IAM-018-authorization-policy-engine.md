# V1-IAM-018 - Authorization Policy Engine

- Task ID: V1-IAM-018
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Politika motoru olarak `authorization_policies` tablosu ile `auto_within(limit_amount, max_count, window)` değerlendirmesi ve limitleri yönetici arayüzünden düzenleme yolu kurulur; karar dokümanının dördüncü bölümünde birinci adımdır.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-018-authorization-policy-engine.md`
- `database/migrations/V1/V1-IAM-018/**`
- `src/Modules/Identity/Authorization/018/**`
- `tests/Modules/Identity/Authorization/018/**`
- `evidence/V1-IAM-018/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-016
- V1-IAM-017

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` sıfır uyarı ile sıfır hata; bu görevin yeni testleriyle ilgili `dotnet test` süzgeci geçer.
- Veri değişiyorsa migration ileri ile geri yönde boş veritabanında denenir.
- Semih için gerçek senaryo: yönetici bir izin için `auto_within` limiti tanımlar; limit içindeki istek kendiliğinden `granted` olur ve `policy_path=auto` yazılır, limiti aşan istek yönetici kararına düşer.

## Handoff

- V1-IAM-019

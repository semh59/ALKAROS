# V1-IAM-018 - Authorization Policy Engine

- Task ID: V1-IAM-018
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Politika motoru: `authorization_policies` tablosu + `auto_within(limit_amount, max_count, window)` degerlendirmesi ve limitleri yonetici arayuzunden duzenleme (model dok. sec. 4 adim 1).

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-018-authorization-policy-engine.md`
- `database/migrations/V1/V1-IAM-018/**`
- `src/Modules/Identity/Authorization/018/**`
- `tests/Modules/Identity/Authorization/018/**`
- `evidence/V1-IAM-018/**`
- Bu gorev, baska bir task'in owned surface alanini degistiremez.

## Dependencies

- V1-IAM-016
- V1-IAM-017

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` 0 uyari / 0 hata; `dotnet test` yetkilendirme filtresi bu gorevin yeni testleriyle gecer.
- Veri degisiyorsa migration ileri/geri bos veritabaninda denenir.
- Semih icin gercek senaryo: yonetici bir izin icin `auto_within` limiti tanimlar; limit icindeki istek otomatik `granted` (`policy_path=auto`), asan istek yonetici kararina dusrer.

## Handoff

- V1-IAM-019

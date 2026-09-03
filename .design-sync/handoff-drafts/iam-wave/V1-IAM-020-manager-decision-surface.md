# V1-IAM-020 - Manager Decision Surface

- Task ID: V1-IAM-020
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Yonetici push + onay/ret yuzeyi: bekleyen istek vardiyadaki her `manager`/`supervisor` cihazina tam baglam blokuyla (masa, tutar, `reason_code`, isteyenin oran anlik goruntusu) dusrer; ilk yanit kazanir. PosTerminal + WaiterPwa yonetici gorunumu.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-020-manager-decision-surface.md`
- `database/migrations/V1/V1-IAM-020/**`
- `src/Modules/Identity/Authorization/020/**`
- `tests/Modules/Identity/Authorization/020/**`
- `evidence/V1-IAM-020/**`
- Bu gorev, baska bir task'in owned surface alanini degistiremez.

## Dependencies

- V1-IAM-019

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` 0 uyari / 0 hata; `dotnet test` yetkilendirme filtresi bu gorevin yeni testleriyle gecer.
- Veri degisiyorsa migration ileri/geri bos veritabaninda denenir.
- Semih icin gercek senaryo: iki yonetici ayni istegi gorur; biri onaylar -> digerinde istek kapanir; grant `policy_path=manual`, `approver_user_id` kayitli.

## Handoff

- V1-IAM-021

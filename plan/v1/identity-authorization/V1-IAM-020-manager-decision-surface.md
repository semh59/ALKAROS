# V1-IAM-020 - Manager Decision Surface

- Task ID: V1-IAM-020
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Yönetici bildirim ile onay ya da ret yüzeyi: bekleyen istek, vardiyadaki her `manager` ile `supervisor` cihazına tam bağlam bloğuyla düşer (masa, tutar, `reason_code`, isteyenin oran anlık görüntüsü) ve ilk yanıt kazanır; PosTerminal ile WaiterPwa yönetici görünümü.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-020-manager-decision-surface.md`
- `database/migrations/V1/V1-IAM-020/**`
- `src/Modules/Identity/Authorization/020/**`
- `tests/Modules/Identity/Authorization/020/**`
- `evidence/V1-IAM-020/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-019

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` sıfır uyarı ile sıfır hata; bu görevin yeni testleriyle ilgili `dotnet test` süzgeci geçer.
- Veri değişiyorsa migration ileri ile geri yönde boş veritabanında denenir.
- Semih için gerçek senaryo: iki yönetici aynı isteği görür, biri onaylayınca istek diğerinde kapanır; yetki kaydında `policy_path=manual` ile `approver_user_id` bulunur.

## Handoff

- V1-IAM-021

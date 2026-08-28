# V1-GOV-020 - First-run provisioning custody

- Task ID: V1-GOV-020
- Status: Done
- Assignee: /root
- Work type: documentation
- Surface state: Existing

## Goal

Temiz container kurulumunda hiçbir user/role/permission bulunmadığı için login'in ulaşılamaz kalması bulgusunu ayrı
remediation sahipliğine bağlamak; bloklu release görevinin exact container/bootstrap yüzeylerini yeni göreve devretmek.

## Owned surface

- `plan/v1/remediation/V1-RMD-031-complete-containerized-release.md`
- `plan/v1/remediation/V1-RMD-033-first-run-manager-provisioning.md`
- `evidence/V1-GOV-020/**`

## Dependencies

- V1-RMD-030

## Acceptance evidence

- `V1-RMD-033` yalnız first-run credential, manager role/permission ve container ordering yüzeylerini sahiplenir.
- `V1-RMD-031` ile production dosya çakışması kalmaz; `V1-RMD-031` kapanışı `V1-RMD-033` sonucuna bağlanır.
- Plan validator sıfır hata ve sıfır uyarı verir.

## Handoff

- V1-RMD-033

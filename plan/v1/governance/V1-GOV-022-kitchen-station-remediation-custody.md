# V1-GOV-022 - Kitchen station remediation custody

- Task ID: V1-GOV-022
- Status: Done
- Assignee: /root
- Work type: documentation
- Surface state: Existing

## Goal

`RMD032-F001` production blocker'ı için Host'un sipariş gönderim istasyonu ile kitchen UI sorgu istasyonunu tek
authoritative runtime contract'ında birleştirecek exact, çakışmasız remediation sahipliğini oluşturmak.

## Owned surface

- `plan/v1/governance/V1-GOV-022-kitchen-station-remediation-custody.md`
- `plan/v1/remediation/V1-RMD-009-sql-persistence-concurrency-and-invariants.md`
- `plan/v1/remediation/V1-RMD-019-kitchen-operations-workspace-ui.md`
- `plan/v1/remediation/V1-RMD-031-complete-containerized-release.md`
- `plan/v1/remediation/V1-RMD-034-authoritative-kitchen-station-contract.md`
- `plan/v1/README.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `evidence/V1-GOV-022/**`

## Dependencies

- V1-RMD-032

## Acceptance evidence

- `RMD032-F001` exact code/test yolları historical sahiplerden tarihli transfer notuyla çıkarılır; historical görev
  status, assignee, goal ve kapanış iddiaları değiştirilmez.
- `V1-RMD-034` yalnız runtime station configuration, UI consumption ve gerçek submit-to-kitchen görünürlük testini
  sahiplenir; bill split, floor plan, reservation, merge, katalog ve dış release blocker'larını kapsamına almaz.
- Plan validation ownership, dependency, section veya language hatası üretmez; audit manifest/report deterministik
  olarak yeniden üretilip doğrulanır.

## Handoff

- V1-RMD-034

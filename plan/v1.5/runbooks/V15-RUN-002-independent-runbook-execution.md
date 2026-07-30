# V15-RUN-002 - Verify operational runbooks independently

- Task ID: V15-RUN-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.38
- PDF:I.41-I.43

## Goal

Critical operational runbook'ları yazar müdahalesi olmadan test ortamında uygulayıp recovery sonuçlarını doğrulamak.

## Owned surface

- `evidence/v15/runbooks/V15-RUN-002/**`
- Bu görev runbook içeriğini veya production code'u değiştiremez.

## In scope

- Printer, payment unknown, fiscal failure, backup, restore, disk ve provider outage runbook execution.

## Out of scope

- Runbook yazımı, defect fix ve production intervention.

## Dependencies

- V15-RUN-001

## Deliverables

- Her critical runbook için tarihli bağımsız execution transcript'i ve result kaydı.
- Başarısız adımlar için kesin defect task ve blocker referansı.

## Acceptance evidence

- Assignee, `V15-RUN-001` assignee'sinden farklıdır; her critical runbook beklenen safe recovery sonucunu üretir.

## Handoff

- V20-DRL-001
- V20-REL-001

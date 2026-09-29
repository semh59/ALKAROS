# V1-RMD-454 - Master push koşusunda Task ID'leri push'taki tüm commit'lerden topla

- Task ID: V1-RMD-454
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-29

## Goal

`Task scope enforcement` job'u master'a birleştirme push'unda yalnız head commit mesajındaki Task ID'lere bakıyor,
oysa fark push öncesinden itibaren PR'ın bütün commit'lerini kapsıyor. Bu yüzden #12, #13 ve #14 birleştirmeleri
master'da kırmızı düştü (ör. #14: yalnız V1-RMD-452 bulundu, V1-RMD-453 dosyaları "kapsam ihlali" sayıldı).
Push olayında ID'ler push'taki tüm commit mesajlarından toplanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-454-task-scope-push-task-ids.md`
- `evidence/V1-RMD-454/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan):
  .github/workflows/task-scope.yml
  plan/AUDIT_MANIFEST.json
  plan/AUDIT_REPORT.md

## In scope

- `Resolve Task ID(s)` adımında push olayı için `github.event.commits[*].message` (ve head commit) taranır.
- PR ve workflow_dispatch davranışı değişmez.

## Out of scope

- task_scope_tool.py'nin kendisi, diğer job'lar ve iş akışındaki başka adımlar.

## Dependencies

- V1-RMD-394

## Acceptance evidence

- `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata, sıfır uyarı.
- Bu görevin master push koşusunda `Task scope enforcement` job'u yeşil.

## Handoff

- None

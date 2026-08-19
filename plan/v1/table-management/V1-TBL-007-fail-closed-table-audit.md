# V1-TBL-007 - Fail-closed table transaction and audit integrity

- Task ID: V1-TBL-007
- Status: Done
- Assignee: Antigravity-v1-tbl-007
- Work type: implementation
- Surface state: Existing

## Goal

Table modülü depolarındaki (`TableTransfer`, `TableMerge`, `Reservations`, `CurrentPointers`) 42P01 (undefined_table)
yutma davranışını kaldırarak eksik şema veya audit hatasında domain işlemini fail-closed iptal etmek ve rollback
sağlamak.

## Owned surface

- `src/Modules/Tables/CurrentPointers/**`
- `src/Modules/Tables/Reservations/**`
- `src/Modules/Tables/TableMerge/**`
- `src/Modules/Tables/TableTransfer/**`
- `tests/Modules/Tables/TableTransfer/**`
- `tests/Modules/Tables/TableMerge/**`
- `tests/Modules/Tables/Reservations/**`
- `tests/Modules/Tables/CurrentPointers/**`
- `evidence/V1-TBL-007/**`

## Dependencies

- V1-TBL-001
- V1-TBL-002
- V1-TBL-003
- V1-TBL-004
- V1-TBL-005
- V1-OPS-001

## Acceptance evidence

- Audit event kaydı veya allocation tablosu eksik olduğunda masa işlemlerinin hata fırlattığı ve transaction'ın rollback
  edildiği testlerle doğrulanır.
- `dotnet test tests/Modules/Tables/` exit 0 verir.
- `task_scope_tool.py --task-id V1-TBL-007` exit 0 verir.

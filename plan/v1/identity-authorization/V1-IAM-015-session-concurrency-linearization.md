# V1-IAM-015 - Device session reconnect and revocation transactional concurrency

- Task ID: V1-IAM-015
- Status: Done
- Assignee: Antigravity-v1-iam-015
- Work type: implementation
- Surface state: Existing

## Goal

DeviceSessionService içinde session doğrulaması, süre kontrolü ve reconnect claim insert işlemlerini tek bir veritabanı
transaction'ı ve satır kilidi altında atomikleştirmek; eşzamanlı `RevokeAsync` araya girdiğinde revoke edilmiş session'a
claim verilmesini engellemek.

## Owned surface

- `src/Modules/Identity/DeviceSessions/DeviceSessionService.cs`
- `src/Modules/Identity/DeviceSessions/IDeviceSessionRepository.cs`
- `src/Modules/Identity/DeviceSessions/PostgresDeviceSessionRepository.cs`
- `tests/Modules/Identity/DeviceSessions/DeviceSessionServiceTests.cs`
- `evidence/V1-IAM-015/**`

## Dependencies

- V1-IAM-001
- V1-IAM-002
- V1-IAM-003
- V1-IAM-007
- V1-IAM-012

## Acceptance evidence

- Reconnect claim ve revoke yarış durumunda revoke edilen session'ın kesinlikle claim alamadığı doğrulanır.
- `dotnet test tests/Modules/Identity/DeviceSessions/` exit 0 verir.
- `task_scope_tool.py --task-id V1-IAM-015` exit 0 verir.

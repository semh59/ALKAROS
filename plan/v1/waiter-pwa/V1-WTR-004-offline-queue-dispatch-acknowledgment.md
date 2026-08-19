# V1-WTR-004 - Waiter PWA offline queue dispatch acknowledgment

- Task ID: V1-WTR-004
- Status: Done
- Assignee: Antigravity-v1-wtr-004
- Work type: implementation
- Surface state: Existing

## Goal

WaiterOfflineQueueEngine içinde mock-success silme mantığını kaldırarak kuyruktaki işlemlerin yalnız sunucudan kesin
başarılı ack alındığında silinmesini sağlamak; dispatcher hatası veya eksikliğinde bağımlı işlemleri bekletmek.

## Owned surface

- `src/Clients/WaiterPwa/SessionQueue/**`
- `src/Clients/WebPrototype/**`
- `tests/Clients/WaiterPwa/SessionQueue/**`
- `tools/run_e2e_browser_test.js`
- `evidence/V1-WTR-004/**`

## Dependencies

- V1-WTR-001
- V1-WTR-002
- V1-WTR-003

## Acceptance evidence

- Dispatcher yokken veya ağ hatasında kuyruk öğelerinin silinmediği ve sonraki öğelerin bloklandığı test edilir.
- `dotnet test tests/Clients/WaiterPwa/` exit 0 verir.
- `task_scope_tool.py --task-id V1-WTR-004` exit 0 verir.

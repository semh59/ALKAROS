# V1-ORD-004 - Idempotent order submission request hash lifecycle

- Task ID: V1-ORD-004
- Status: Done
- Assignee: Antigravity-v1-ord-004
- Work type: implementation
- Surface state: Existing

## Goal

SubmitOrderHandler içinde expired idempotency anahtarı yeni bir sipariş payload'ı ile kullanıldığında `request_hash`
ve `response_envelope` değerlerini atomik güncellemek; eski payload ile replay veya conflict durumlarını deterministik
yönetmek.

## Owned surface

- `src/Modules/Orders/SubmitOrder/**`
- `src/Modules/Orders/ItemExceptions/**`
- `tests/Modules/Orders/SubmitOrder/**`
- `tests/Modules/Orders/ItemExceptions/**`
- `evidence/V1-ORD-004/**`

## Dependencies

- V1-ORD-001
- V1-ORD-002
- V1-ORD-003
- V1-FND-002
- V1-FND-018

## Acceptance evidence

- Expired idempotency anahtarının yeni payload ile yenilendiğinde `request_hash`'in güncellendiği ve sonraki aynı yeni
  istekte replay edildiği test edilir.
- Expired yenileme sonrası eski payload ile yapılan çağrının conflict ürettiği test edilir.
- `dotnet test tests/Modules/Orders/` exit 0 verir.
- `task_scope_tool.py --task-id V1-ORD-004` exit 0 verir.

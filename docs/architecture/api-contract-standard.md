# API and Event Contract Standard — approved decision record

> **Task:** V0-ARC-004
> **Status:** Done
> **Source basis:** PDF:I.0-I.5, PDF:I.2 (I.2.1-I.2.3), PDF:I.15, PDF:II.0-II.1, PDF:II.5, PDF:III.0-III.2,
> PDF:III.1.6, PDF:III.8.2
> **Access date:** PDF source 2026-07-29; artifact verification 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (PDF baseline + named approver)

## PDF-locked constants (not negotiable)

1. **Contract language English** — endpoints, events, commands, queries,
   DTOs, migration and test names are English (PDF:I.2, I.2.1; forbidden
   examples I.2.3).
2. **UI language Turkish** — user-facing messages, help text and legally
   required Turkish fiscal/customer documents (PDF:I.2.2, I.1.2).
3. **Technology stack** — C# / .NET 10 LTS, EF Core 10, PostgreSQL 18,
   React + TypeScript, waiter PWA, SignalR realtime (PDF:I.1.2).
4. **Idempotency first-class** — `IdempotencyKey`, `ExternalReference`,
   `ProviderEventId` used where appropriate (PDF:I.15); database-level
   `idempotency_key` + partial unique index for payment allocations
   (PDF:III.8.2, CORR:C4).
5. **Optimistic concurrency** — `row_version` token (PDF:III.1.6).
6. **Single source of truth** — PostgreSQL is authoritative; SignalR,
   printer buffers, provider callbacks, QR sessions and UI state are not
   (PDF:II.1.1).

## Approved selections (2026-08-03)

1. **URL versioning: path-based.** Public endpoints carry a major version
   segment: `GET /api/v1/orders`. Version is part of the URL; header/query
   versioning is not used.
2. **Request validation: FluentValidation.** Every public command/query has a
   validator in the owning module. FluentValidation dependency is introduced
   by the first API implementation task (V1-FND-001 or V1-CAT-001).
3. **Headers: `X-` prefixed corporate set.**
   - `X-Idempotency-Key` — required on all mutating commands (replay-safe).
   - `X-Row-Version` — optimistic concurrency token on updates/deletes.
   - `X-Correlation-Id` — request correlation; echoed in responses and logs.
   - `X-Request-Id` — server-assigned request identifier.
4. **Pagination: cursor-based.** List endpoints accept `?cursor=` (opaque
   base64) and `?limit=`; responses return `next_cursor` (null when no more).
   First page is requested without cursor.
5. **Error response: custom envelope + catalogue.**

    ```json
    {
      "error": {
        "code": "ORDER_NOT_FOUND",
        "message": "Sipariş bulunamadı.",
        "details": [ ... ],
        "traceId": "...",
        "status": 404
      }
    }
    ```

    `code` is a stable machine-readable key from the catalogue
   (`docs/engineering/error-code-catalogue.md` — owned by the first API
   implementation task); `message` is user-facing Turkish; `details` optional
   per-failure diagnostics; envelope is JSON.
6. **Event schema: CloudEvents.** Domain/integration events are published
   with a CloudEvents envelope (`id`, `source`, `type`, `time`, `datacontenttype`,
   `data`). Event type names are English, reverse-domain or module-scoped
   (e.g. `order.order_state_changed.v1`).

## Change-breaking rule

- **Additive changes** (new optional field, new endpoint, new event type)
  are compatible within a major version.
- **Breaking changes** (field removal, type change, semantic change,
  rename) require a new major version in the URL segment and a new event
  type version; the old major version remains served until the consuming
  modules have migrated.
- No public contract changes without a decision record and affected-task
  list.

## Example contract 1 — `POST /api/v1/orders`

Request:

```http
POST /api/v1/orders
X-Idempotency-Key: 9f1c...e2
X-Correlation-Id: 3a0b...77
Content-Type: application/json

{ "channel": "Waiter", "tableId": 12, "items": [ { "productId": 101, "quantity": 2 } ] }
```

Deterministic semantics:

- Success → `201` with order resource (includes `rowVersion`).
- Validation failure → `400` envelope `code: VALIDATION_FAILED`.
- Replay of the same `X-Idempotency-Key` returns the original `201` body
  without creating a second order (idempotent, PDF:I.15).
- Concurrent conflicting update → `409` envelope `code: CONCURRENT_MODIFICATION`.

## Example contract 2 — `POST /api/v1/bills/{billId}/payments`

Request:

```http
POST /api/v1/bills/55/payments
X-Idempotency-Key: b2d0...aa
X-Row-Version: 7
Content-Type: application/json

{ "method": "Cash", "tenderedAmount": 1000.00 }
```

Deterministic semantics:

- Success → `201` payment + allocation summary; `changeAmount` returned
  separately, never as a negative allocation (PDF:I.13.1).
- Insufficient remaining payable → `422` envelope `code: ALLOCATION_EXCEEDS_PAYABLE`
  (PDF:II.6.6).
- Replay with same key → original `201`; duplicate callback cannot create
  duplicate allocations (PDF:III.8.2).
- Stale `X-Row-Version` → `409` `code: CONCURRENT_MODIFICATION`.

## Rejected alternatives

1. Header-based, query-based versioning — URL segment chosen (cache/proxy
   friendly, explicit).
2. DataAnnotations + filters, custom Result model — FluentValidation chosen
   by approver.
3. Standard HTTP headers (`Idempotency-Key`, `If-Match`) — corporate `X-`
   set chosen by approver.
4. Offset/limit, keyset pagination — cursor chosen by approver.
5. RFC 9457 Problem Details, plain `{message}` — custom envelope + catalogue
   chosen by approver.
6. Custom envelope without schema, contract-only events — CloudEvents chosen
   by approver.

## Affected tasks

- Dependencies: V0-ARC-001 (plan change C38, 2026-08-03: V0-ARC-003 removed).
- Consumers: V0-ARC-006, V0-ARC-008, V0-SEC-001, V0-DOC-001, V12-PAY-002,
  V20-DOC-002.
- Handoff: None.

## Acceptance evidence

- Two example contracts with deterministic success/error/replay semantics and
  the change-breaking rule: above.
- Decision record with source, access dates, approver, rejected alternatives
  and affected task IDs: above.

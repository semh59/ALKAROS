# API and Event Contract Standard

> **Task:** V0-ARC-004
> **Status:** InProgress
> **Assignee:** codex-v0-arc-004
> **Work type:** decision
> **Source basis:** PDF:I.0-I.5, PDF:II.0-II.1, PDF:III.0-III.2
> **Date:** 2026-07-30

## 1. HTTP API Standard
- Versioning: URL path (`/api/v1/...`), breaking changes require new version.
- Validation: FluentValidation, 400 Bad Request with field-level errors.
- Error format: RFC 7807 ProblemDetails.
- Idempotency: `Idempotency-Key` header (per ARC-003).
- Concurrency: `If-Match` header with ETag for optimistic concurrency.
- Pagination: `?page=1&pageSize=50`, response includes `total`, `page`, `pageSize`.

## 2. Event Contract Standard
- Event naming: `<Module>.<Entity>.<Action>` (e.g., `Billing.Bill.Settled`).
- Event payload: JSON with `eventId`, `eventType`, `occurredAt`, `version`, `data`.
- Schema: JSON Schema validation on publish and consume.
- Versioning: Additive only (new fields optional); breaking changes require new event type.

## 3. Error Codes
- `400` Validation error, `401` Unauthorized, `403` Forbidden, `404` Not found, `409` Conflict, `422` Business rule violation, `500` Server error.

## 4. Affected Tasks
- None
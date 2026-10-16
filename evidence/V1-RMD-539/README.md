# V1-RMD-539 - Error management API

Endpoints under `/api/v1/management/observability/errors`: list (layer/severity/status filter, paging max 100), detail (last 20 occurrences), acknowledge, resolve (manage permission + audit event), notification-channel status.

Verified by `test-runs.log`: 5/5 endpoint tests on real Postgres (filters, injection-safe filters, status transitions, 10k events first page < 300 ms, unauthenticated call rejected) and DualScreenHostTests 6/6.
Mutations (`mutations.log`): status/layer filter, ack/resolve state guards, auth filter and page-size clamp mutants are killed.

Not verified: the acknowledge/resolve success path through HTTP with a real manager session (only the query layer and the unauthenticated rejection are tested); the screen is V1-RMD-565.

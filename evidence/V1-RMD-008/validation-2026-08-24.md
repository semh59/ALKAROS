# V1-RMD-008 validation — 2026-08-24

Candidate commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`

## Disposable build

The candidate tree was exported to a disposable directory, the V1-RMD-008 owned
files were overlaid, and the original read-only `.git` directory was mounted so
the repository-commit build gate remained active.

```text
mcr.microsoft.com/dotnet/sdk:10.0.302
dotnet restore tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --locked-mode
dotnet build tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj -c Release --no-restore
Build succeeded.
0 Warning(s)
0 Error(s)
Exit code: 0
```

## Real HTTP and PostgreSQL integration tests

Server image:
`postgres@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2`.
The database container, database, and credentials were ephemeral; credentials
were not written to output or evidence. The test runner used the .NET 8.0.419
runtime because the test target is `net8.0`.

```text
psql (PostgreSQL) 15.19 (Debian 15.19-0+deb12u1)
VSTest version 17.11.1 (x64)
A total of 1 test files matched the specified pattern.
Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10, Duration: 6 s
Exit code: 0
```

Covered real Kestrel behaviors:

- untrusted `X-Forwarded-For` / `X-Forwarded-Proto` cannot change effective
  scheme and plain production HTTP returns `400 HTTPS_REQUIRED`;
- a configured proxy can establish effective HTTPS;
- client-IP pairing partitions are independent, the eleventh same-client
  request returns `429`, and `Retry-After` is a positive lease-derived value;
- HTTPS-effective login issues a `Secure`, `HttpOnly`, `SameSite=Strict` cookie;
- an authenticated cashier without `pos.cashier.mutate` receives `403` and an
  `identity.denial_events` row is persisted before rejection;
- the same cookie receives `401` after server-side revocation;
- a display cookie receives `403` on a cashier mutation;
- customer-display DTO allowlist tests remain green.

## Continuation closure — 2026-08-25

The error middleware used by production `Build` was registered in a test-only
Kestrel application. A test-only endpoint declared a 1024-byte response, sent
headers and one byte, then threw. The client observed the truncated transport,
and the captured production middleware log contained EventId `5000`, error
level, the endpoint path, the exact response trace ID, and the exception before
the connection was aborted.

```text
ResponseStartedFailureIsLoggedWithTraceIdBeforeKestrelAbortsTheConnection
Failed: 0, Passed: 1, Skipped: 0, Total: 1
Exit code: 0
```

For the final database run, both server and every `psql` invocation used the
same digest-pinned PostgreSQL image on a task-local isolated Docker network. A
read-only named volume supplied migration scripts to the disposable client
container. Containers, network, volumes, and credentials were removed after
the run.

```text
psql (PostgreSQL) 18.6
server_version_num=180006
VSTest version 17.11.1 (x64)
Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 35 s
Exit code: 0
```

```text
dotnet format src/Host/ALKAROS.Host.csproj --verify-no-changes --no-restore --include <owned Host files>
dotnet format tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --verify-no-changes --no-restore --include <owned test files>
Exit code: 0
```

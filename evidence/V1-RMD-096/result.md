# V1-RMD-096 - Host-terminated HTTPS result

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Closes the pre-go-live review finding **E1** (Host listened only on
  `http://0.0.0.0:5080`).

## Problem

The waiter PWA needs a secure context for its service worker / offline queue.
That was supplied only by the Caddy reverse proxy, and only if
`--trusted-network` matched the real Docker network so the host would honour
`X-Forwarded-Proto: https`. A published `5080`, a wrong CIDR, or a missing proxy
would silently break offline mode, and the image had no standalone HTTPS story.

## Change

`serve` now accepts multiple `;`-separated `--urls`; when any is `https://`,
Kestrel terminates TLS itself.

| Flag | Effect |
| --- | --- |
| `--tls-cert` / `--tls-key` | load a mounted PEM certificate (approved public / internal-CA), round-tripped through PKCS#12 |
| `--self-signed-host <host>` | generate a self-signed cert (SAN: host + `localhost` + `127.0.0.1` + `::1`), cached under `<content-root>/tls`, regenerated only when missing or within 30 days of expiry |

`DualScreenOptions` fail-closes: an `https://` URL with neither source is
rejected; `--tls-cert` without `--tls-key` is rejected; `--self-signed-host`
without an `https://` URL is rejected. `DualScreenApplication.Build` binds the
resolved certificate via `ConfigureKestrel(ConfigureHttpsDefaults(...))`.

- `Dockerfile`: `EXPOSE 5080 5443`; ENTRYPOINT binds
  `http://0.0.0.0:5080;https://0.0.0.0:5443 --self-signed-host localhost`; a
  comment explains the plain-HTTP port is only for a trusted TLS-terminating
  proxy.
- `compose.yaml`: `alkaros-host-tls` volume; publishes `8444:5443` as a fallback
  (Caddy on `8443` stays primary); `--self-signed-host ${ALKAROS_PROXY_HOST}`.
- `deploy/docker/README.md`: the two independent TLS paths, `--tls-cert` for a
  real certificate, device cert install + field-test steps.

## Tests

`tests/Host/MigrationComposition/DualScreen/` — `dotnet test` DualScreen filter
**27 passed / 0 failed**:

- `DualScreenOptionsTests`: `HttpsUrlWithoutACertificateSourceIsRejected`,
  `SelfSignedHostRequiresAnHttpsUrl`, `TlsCertificateAndKeyMustBeSuppliedTogether`,
  `DualHttpAndHttpsBindingWithSelfSignedHostParses`.
- `DualScreenHostTests.HostTerminatesHttpsWithASelfSignedCertificateAndNeedsNoForwardedProto`:
  builds with `https://127.0.0.1:0 --self-signed-host pos.lan`, starts, makes an
  HTTPS `GET /` with **no** `X-Forwarded-Proto` header → 200 (the HTTPS_REQUIRED
  gate does not fire because the request is genuinely HTTPS), and the presented
  leaf certificate's SAN contains `pos.lan` and `localhost`.
- `DualScreenHostTests.SelfSignedCertificateForLocalhostDoesNotDuplicateSubjectAlternativeNames`:
  `--self-signed-host localhost` yields a SAN with `localhost` exactly once
  (the DNS/IP SAN builder deduplicates).

Full suite: `dotnet test ALKAROS.slnx` 46 projects / 1100 passed / 0 failed.

## Live check (`https-live-check.log`)

Host DLL run with `--urls http://0.0.0.0:5080;https://0.0.0.0:5443
--self-signed-host pos.lan` against the composed database:

```text
--- HTTPS (direct, no forwarded header) GET / ---   http_code=200
--- HTTP without forwarded proto GET / ---          http_code=400   (HTTPS_REQUIRED)
--- HTTPS cert SAN ---
    DNS:pos.lan, DNS:localhost, IP Address:127.0.0.1, IP Address:0:0:0:0:0:0:0:1
```

## Out of scope

Removing the reverse proxy (Caddy stays the primary TLS path); ACME / public
certificate automation (mount one with `--tls-cert`); streaming replication.

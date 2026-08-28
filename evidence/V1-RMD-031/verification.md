# V1-RMD-031 verification

- Candidate commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Candidate tree: `39e9bb79d3d6f3e099e15a7ffafcc801f727843a`
- Build image: `sha256:d2780e802c408f4b080f4a7d5185065ef4c0d8f2d1aa541f4e48a7c00c70bcfc`
- Build input hashes: `.dockerignore` `6FA12B01A03EEBDAA751A4D4CCF7B71B4D9C3D391937770348F3D84067E0055D`, Dockerfile `F1F76B59B03203AEA8467A96031B39CD7B314A1D1D0536A4FA75469781A65DC1`, compose `CF7F26BB9AE26D9664CC361E44B2DA8FF272C601BE1FADEEC0BF0B30CFB8E97B`, Caddyfile `16182ADED9F5898B4A9ECE647B8507EA549F89474875922F4D6695C6CB529247`.

## Passed

- `docker build --progress=plain -t alkaros:local-audit .` — exit 0. The build uses SDK `10.0.302`, Node `24.6.0-bookworm-slim`, pnpm `11.19.0`, locked restore, deterministic Git provenance, publish-only Host output and production PosTerminal bundle.
- `docker build --no-cache --progress=plain --target host-build -t alkaros:host-clean-audit .` — exit 0. A cacheless checkout reproduced the Git provenance-checked restore and Release publish.
- `python -m pytest tests/Deployment/test_container_contract.py -q` — 4 passed; the only warning was the pre-existing inaccessible pytest cache directory.
- `python -B tools/plan-audit/plan_audit_tool.py validate` — exit 0, 0 errors, 0 warnings (435 Markdown files, 413 task files).
- `docker compose config --quiet` with no password or station environment — exit 0. The password is loaded from the mounted secret inside each process and is absent from Compose `Config.Env`; the station has the functional `kitchen-main` default.
- Disposable `docker compose up -d --build` — PostgreSQL 18 digest image healthy, migration service exited 0 after all 38 positions, Host healthy, proxy started.
- In-container readiness probe — `curl --fail http://127.0.0.1:5080/health/ready` returned `{"status":"Ready"}` (HTTP 200).
- Migration verification — `select count(*) from public.alkaros_schema_migrations` returned `38`.
- Host restart — service returned healthy and readiness 200; migration count remained `38`, proving named-volume persistence in the disposable run.
- Final image inspection — `/app/ALKAROS.Host.dll`, `/app/wwwroot/index.html`, and `/app/database/MigrationComposition/order.json` exist; no `/app` `bin`, `obj`, `node_modules`, or `dist` directories exist.
- `docker compose down --volumes --remove-orphans` completed; the disposable database and volumes were removed.
- Docker Scout SBOM indexed 52 packages and reported `No vulnerable package detected` after the Alpine/OpenSSL patch.
- Runtime correction reproduced the original TLS failure: hostname-free `:443` caused a Caddy TLS alert `internal error` and browser `ERR_SSL_PROTOCOL_ERROR`. Binding the site to `https://localhost` produced a SAN/SNI identity, TLS 1.3 completed, and `/health/ready` plus `/` returned HTTP 200 through port 8443.
- Windows CRLF secret handling was verified by length/SHA-256 without revealing the secret. Shell `cat` retained the trailing carriage return and caused PostgreSQL authentication failure; `tr -d '\r\n'` normalized CRLF/LF for both migration and Host. A full environment-free `docker compose up --detach --force-recreate --remove-orphans --wait` then exited 0.
- The persistent stack now uses Host image `sha256:f014e3418f68a3b466d1f6fc71304267ff3fbeefd55b13628351a4adabddc5b4` and migration image `sha256:6351ba58d5500298541175ec99ea5a00f3052cec0f8f40967ab51f0d27b516f4`. PostgreSQL, Host and proxy are healthy; migration exited 0. Restarting PostgreSQL, Host and proxy preserved all 38 migration records and returned HTTPS 200 for health and the production UI asset.
- Stale standalone containers `alkaros-rmd025-zoom-pg-20260827`, `alkaros-rmd020-http`, and `alkaros-test-pg` were removed after exact target and mount inspection. FABRIC and LOJINEXT containers were not touched. The stable Compose project is named `alkaros`.
- The live deployment secret remains available for container restart but is excluded by both `.dockerignore` and `deploy/docker/.gitignore`; `db_password.example` remains trackable. The secret value was never copied into evidence.

## Production blockers

- The Caddyfile uses a functional `localhost` certificate with `tls internal` for local verification only. The in-app browser now reaches the certificate boundary and reports `ERR_CERT_AUTHORITY_INVALID`, replacing the remediated protocol failure. Installing the local CA into the Windows trust store was not performed. An approved deployment certificate/trust policy and independent real HTTPS browser E2E evidence are still required before external go-live.
- Fiscal device, physical printer, QNB, Yemeksepeti, meal-card, QR relay, backup/RPO-RTO, licensing, security assessment, and signed go-live evidence were not available and were not fabricated.
- `docker sbom` is not installed in this Docker Desktop environment; Docker Scout's generated SBOM was used for the available scan and must be retained by CI with an approved scanner.

Conclusion: the containerized local release path is reproducible and operational, but the repository remains `NOT PRODUCTION READY` until the blockers above are remediated and independently re-verified.

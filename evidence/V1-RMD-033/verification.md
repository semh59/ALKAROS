# V1-RMD-033 verification

- Verified at: `2026-08-28T13:59:22+03:00`
- Candidate commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Candidate tree before the task write-set: `39e9bb79d3d6f3e099e15a7ffafcc801f727843a`
- Repository root: `D:\PROJECT\ALKAROS`
- Reviewer/implementer: `/root`
- Secret handling: the generated operational manager password and database password are intentionally absent from this evidence.

## Final artifacts

| Path | SHA-256 |
| --- | --- |
| `.dockerignore` | `8ac90c76fe0f51c2ed56b7047e57d7422b5c0a17372f886a2e312e2ffff2d852` |
| `compose.yaml` | `7dc3986c49c803fa695236be8d6550d1a84c7bd4d15dbea4ccdbc73f0e7ef207` |
| `deploy/docker/.gitignore` | `02e5e55883a7778854ba3c2af7e0ee508af88a9e59981fd33f215bfd01010841` |
| `deploy/docker/Caddyfile` | `16182aded9f5898b4a9ece647b8507ea549f89474875922f4d6695c6cb529247` |
| `deploy/docker/README.md` | `ad0c16817cf32c90518f1b56601ea81a71d23600e22d2afae41ac7ecbca8f86e` |
| `deploy/docker/admin_password.example` | `22571a79c4d6e8940e8ea5db839ec48787131a7f01cc501e347d38fae99e90fc` |
| `src/Host/Program.cs` | `fef9300b6f956bdf947057a187ca7997a0e7630d6a57a3c76be11c375ad75fea` |
| `tests/Deployment/test_container_contract.py` | `320570b85369cdb48b652514593e1e4e07b4091477aa8a286a3983b55d01319f` |
| `tests/Host/MigrationComposition/Program/FirstRunProvisioningTests.cs` | `60bb05dbb2716f5632f9222231a06cc50d97f8002fe10807d41681f127955c29` |

Final local image IDs:

- `alkaros-host:latest`: `sha256:a6f2d2b6157b08a985a0fac35452469d37a31f313f61ce3f1171efbde7e83ba4`
- `alkaros-migrate:latest`: `sha256:8386c4319e9f264a408047951641abc148de0740853fcf3666b2233b99a0e331`
- `alkaros-provision:latest`: `sha256:e8dbf2fb49eed7efb58787c52e467172229303219e5a77c29af338ae23a43e19`

## Automated verification

1. `docker compose config --quiet`
   - Exit code: `0`.
2. `docker compose build --no-cache --pull`
   - Exit code: `0`.
   - Locked .NET restore, Release Host publish, locked pnpm install and production UI build passed.
3. Isolated focused test run using the pinned .NET SDK 10.0.302 build image and `DOTNET_ROLL_FORWARD=Major` for the repository's `net8.0` test target:
   - `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~FirstRunProvisioningTests`
   - Exit code: `0`; `3/3` passed.
4. Full Host migration-composition suite in an isolated runner with PostgreSQL client and the Compose PostgreSQL 18 service:
   - `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --configuration Release --no-restore`
   - Exit code: `0`; `96/96` passed in 36 seconds.
   - A prior runner-only attempt had `67` passes and `29` setup failures because that runner lacked `psql`; after installing the required client, the unchanged suite passed completely.
5. `python -m pytest tests/Deployment/test_container_contract.py -q`
   - Exit code: `0`; `5/5` passed.
   - Pytest reported only the pre-existing inaccessible `.pytest_cache` warning and did not write that cache.
6. `python -B tools/plan-audit/plan_audit_tool.py validate`
   - Exit code: `0`; `437` Markdown files, `415` task files, `1396` dependency edges, `0` errors, `0` warnings.
7. `python -B tools/plan-audit/plan_audit_tool.py validate-coverage`
   - Exit code: `0`; `383` stored coverage headings, `2903` stored coverage units, `0` errors.

## Clean-volume and provisioning verification

- The exact Compose-managed test volume `alkaros_alkaros-postgres` was verified by labels
  `com.docker.compose.project=alkaros` and `com.docker.compose.volume=alkaros-postgres`, removed, and recreated. No
  Caddy or unrelated Docker volume was removed.
- Clean startup applied all `38` migration positions and then ran `provision` before Host startup.
- Final identity graph: `1` user, `1` manager role, `1` user-role assignment and `2` manager role-permission
  assignments.
- The manager permissions are exactly `catalog.manage,pos.cashier.mutate`.
- Provisioning rerun exited `0`; the existing user ID and PBKDF2 password hash were unchanged and no duplicate user,
  manager role, assignment or permission assignment was created.
- A separate temporary migrated database containing only `existing-user` made `provision-manager` exit `2` with
  `Manager provisioning is refused because users already exist.` The temporary database was then dropped.
- Missing bootstrap password exited `2` with the required-variable message. A five-character bootstrap password
  exited `2` with the 12-to-256-character boundary message.
- The real manager and database secret values were absent from successful logs and all captured failure outputs.
- `docker inspect alkaros-provision-1` showed no database or bootstrap password in `Config.Env`; both entered the
  container as read-only `/run/secrets/...` mounts.
- A full `docker compose down` followed by `docker compose up --detach --force-recreate --remove-orphans --wait`
  preserved the manager user ID, password hash, role and all 38 migration history rows.

## HTTPS browser verification

- The Caddy local root certificate was installed into the Windows Current User trusted-root store only after the
  user's explicit approval. Browser navigation to `https://localhost:8443/` then completed without a certificate
  interstitial.
- A fresh logout/login against the clean PostgreSQL volume succeeded with the secret-backed `admin` account.
- The authenticated DOM exposed the live server status, `ALKAROS Manager`, cashier order surface and links for
  `Masalar`, `Mutfak` and `Menü`.
- Direct visits showed production API-backed empty states and manager actions for table/floor management, kitchen
  operations, and catalog CRUD.
- After full stack restart, browser reload retained the authenticated manager session and all four production routes.

## Final runtime state

- `alkaros-postgres-1`: healthy.
- `alkaros-migrate-1`: exited `0` as designed.
- `alkaros-provision-1`: exited `0` as designed.
- `alkaros-host-1`: healthy.
- `alkaros-proxy-1`: healthy and published only on host port `8443` for HTTPS.
- No old standalone ALKAROS container remained.

## Verdict

`V1-RMD-033` acceptance evidence passes. This closes the first-run login gap for the local containerized release. It
does not waive or close the external production blockers owned by `V1-RMD-031` and the later independent acceptance
task `V1-RMD-032`.

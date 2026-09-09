# ALKAROS container release

The stack is a **frontend / backend split** (deep-analysis A1):

| Service | Image | Role |
| --- | --- | --- |
| `web` | `deploy/docker/Dockerfile` target `web` (Caddy) | Serves the static client bundles (PosTerminal SPA, WaiterPwa, Cashier), **terminates TLS**, and reverse-proxies `/api`, `/hubs`, `/health` to `api`. |
| `api` | `deploy/docker/Dockerfile` target `api` (aspnet 8) | `ALKAROS.Host serve --api-only`: JSON API + SignalR hubs on plain HTTP `:5080`. No static assets, no TLS. |
| `postgres` | `postgres:18` (digest pinned) | Datastore, named `alkaros-postgres` volume, tuned config. |
| `migrate`, `provision` | `api` image, one-shot | Ordered schema migration then idempotent manager provisioning; both exit before `api` starts. |

Operator maintenance tools (`backup`, `basebackup`, `housekeeping`) live in
`compose.ops.yaml` and are **never** started by `up` — see
`docs/recovery/backup-restore-runbook.md` and
`docs/operations/data-housekeeping.md`.

## First run

1. Create `deploy/docker/db_password` with a deployment-only database password.
2. Create `deploy/docker/admin_password` with a unique manager password of 12-256 non-whitespace characters.
3. Optionally export `ALKAROS_BOOTSTRAP_USERNAME`, `ALKAROS_BOOTSTRAP_DISPLAY_NAME` or
   `ALKAROS_KITCHEN_STATION_ID`; defaults are `admin`, `ALKAROS Manager` and `kitchen-main`.
4. Export `ALKAROS_PROXY_HOST` with the host name or LAN IP the devices use (default `localhost`).
5. Run `docker compose up --detach --build --pull always --force-recreate --remove-orphans --wait`.

The one-shot migration/provisioning services and `api` read passwords from mounted Docker secrets; passwords are not
required in the Compose environment. Windows CRLF and Unix LF line endings are normalized without logging the secret.
Migration and idempotent manager provisioning must complete before `api` starts. Re-running provisioning never resets an
existing password, and provisioning refuses takeover when another user exists without the configured manager. Both real
secret files are ignored by Git and the Docker build context.

The Compose project name is fixed to `alkaros`, so rebuilds replace the same service set instead of creating a second
project group when the checkout directory name changes. `--remove-orphans` removes services left by an older ALKAROS
Compose definition (including the previous single-container `host` / `proxy` layout); it does not delete unrelated
standalone containers or their data.

The stack does not claim fiscal, payment-provider, printer or signed go-live readiness.

## TLS

TLS terminates **only at `web`** (Caddy). The `api` service listens on plain HTTP `:5080`, which is not published to
the host and rejects any non-loopback request that does not arrive through the trusted proxy with
`X-Forwarded-Proto: https` (HTTP 400 `HTTPS_REQUIRED`), so it cannot serve the app insecurely.

- **Caddy on `8443`** terminates TLS with its internal CA. Replace `tls internal` in `deploy/docker/Caddyfile` with an
  approved public certificate and a real domain before an external (public-domain) go-live, or mount one and switch the
  directive to `tls /path/cert.pem /path/key.pem`.
- There is **no host-terminated HTTPS fallback**. The earlier `:8444` path (V1-RMD-096) was removed when static serving
  and the secure-context responsibility moved to `web` in the A1 split (V1-RMD-098): the container no longer runs a
  second TLS listener. If Caddy is down the stack is down — run a standby proxy for HA rather than relying on the app to
  self-serve TLS.

## Customer-display origin isolation (finding B-4)

The customer display is a distinct browser origin from the cashier so their `localStorage` — and the cashier terminal
id / bill id — never leak across. `web` enforces this at the proxy:

- `display.<ALKAROS_PROXY_HOST>` proxies only `/api/v1/customer-displays/*` and `/hubs/customer-display/*` to `api`,
  each tagged `X-Alkaros-Origin: display`.
- The main vhost strips any inbound `X-Alkaros-Origin` before proxying, so a client cannot forge the display origin.
- `api` (`serve --api-only --customer-display-origin-header X-Alkaros-Origin`) restricts a request carrying the trusted
  header to the display route allowlist, and refuses those routes on every other origin.

Point the customer displays at `https://display.<ALKAROS_PROXY_HOST>:8443/display`.

## QR/NFC public relay origin scope (relay scope hardening, 2026-09-09)

The Cloudflare Tunnel connector (`ALKAROS.QrRelay.LocalConnector`, provisioned
from Settings → Relay) runs in the same container as `api` and reaches it over
loopback (`RelayProvisioningService`'s own tunnel configuration points at
`http://localhost:5080`) — it never goes through `web`/Caddy at all. Before
this flag, a provisioned tunnel exposed the **entire** application (every
Cashier and manager API included) to the public internet, protected only by
the app's own authentication — a much larger blast radius than intended for a
surface meant to carry only anonymous customer ordering traffic.

`--nfc-loopback-origin` closes that: a loopback-sourced request is now treated
the same as the dedicated-port/header signals above and restricted to the NFC
(and, once built, QR) customer route allowlist. `compose.yaml`'s `api` service
already passes this flag; nothing else needs to change to pick it up. No
inbound port is published for `api` (`compose.yaml` publishes only `web`'s
`8443:443`), so nothing outside this container can reach `5080` at all, let
alone forge a loopback-looking connection to it.

## HTTPS and WaiterPwa offline field readiness

The WaiterPwa offline queue relies on a service worker, and browsers only register a service worker in a secure
context: HTTPS with a certificate the device trusts, or `localhost`. A waiter phone reaching the stack over
`http://<lan-ip>` gets no offline mode, and the app shows `Çevrimdışı mod kapalı` instead of failing silently.

Certificate strategy for a LAN deployment (no public domain):

1. Set `ALKAROS_PROXY_HOST` to the host name or LAN IP the devices use before `docker compose up`, e.g.
   `ALKAROS_PROXY_HOST=192.168.1.50`. Caddy's internal CA then issues a certificate whose SAN matches that address for
   both the main and `display.` virtual hosts.
2. Export the internal root CA and install it on every waiter device and customer display:

   ```sh
   docker compose cp web:/data/caddy/pki/authorities/local/root.crt ./alkaros-root.crt
   ```

   Transfer `alkaros-root.crt` to each device and install it as a trusted CA
   (Android: Settings → Security → Encryption & credentials → Install a certificate → CA certificate;
   iOS: install the profile, then enable full trust under Settings → General → About → Certificate Trust Settings).

3. For an external go-live, replace `tls internal` with an approved public certificate and a real domain.

Field test before go-live (with `ALKAROS_PROXY_HOST` set):

1. `docker compose up --detach --build --wait`.
2. On a waiter phone joined to the same network, open `https://<ALKAROS_PROXY_HOST>:8443` after installing the root CA;
   confirm the padlock shows a trusted connection.
3. Sign in, open a table, turn off WiFi, and enter an order. The status ribbon must read
   `Çevrimdışı • İşlemler Güvenli Kuyrukta`, not `Çevrimdışı mod kapalı`.
4. Turn WiFi back on and confirm the queued order reaches the server and the ribbon returns to `Çevrimiçi`.
5. Open `https://display.<ALKAROS_PROXY_HOST>:8443/display`, pair it, and confirm it reflects the cashier's cart but
   that its browser storage is empty of any cashier terminal / bill id (finding B-4).
6. Bring the api up with its port published (`docker compose -f compose.yaml -f compose.dev.yaml up -d`) and confirm a
   direct `http://<lan-ip>:5080/` request is refused with `HTTPS_REQUIRED`.

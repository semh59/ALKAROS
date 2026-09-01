# ALKAROS container release

1. Create `deploy/docker/db_password` with a deployment-only database password.
2. Create `deploy/docker/admin_password` with a unique manager password of 12-256 non-whitespace characters.
3. Optionally export `ALKAROS_BOOTSTRAP_USERNAME`, `ALKAROS_BOOTSTRAP_DISPLAY_NAME` or
   `ALKAROS_KITCHEN_STATION_ID`; defaults are `admin`, `ALKAROS Manager` and `kitchen-main`.
4. Run `docker compose up --detach --build --pull always --force-recreate --remove-orphans --wait`.

The one-shot migration/provisioning services and Host read passwords from mounted Docker secrets; passwords are not
required in the Compose environment. Windows CRLF and Unix LF line endings are normalized without logging the secret.
Migration and idempotent manager provisioning must complete before Host starts. Re-running provisioning never resets an
existing password, and provisioning refuses takeover when another user exists without the configured manager. Both real
secret files are ignored by Git and the Docker build context. PostgreSQL 18 is digest pinned and uses the named
`alkaros-postgres` volume.

TLS has two independent paths (V1-RMD-096):

- **Caddy on `8443`** terminates TLS and forwards trusted proxy headers to Host on
  the plain-HTTP port `5080`. This is the primary path. The certificate is Caddy's
  internal CA certificate; replace `tls internal` in `deploy/docker/Caddyfile` with
  an approved public certificate before an external (public-domain) go-live.
- **Host on `8444`** — Host itself terminates HTTPS on `5443` with a self-signed
  certificate whose SAN is `ALKAROS_PROXY_HOST` (plus `localhost` / `127.0.0.1`),
  regenerated and cached in the `alkaros-host-tls` volume. This is a fallback so a
  waiter phone keeps a secure context — and therefore a working service worker /
  offline queue — even if Caddy is down or its `X-Forwarded-Proto` header is lost.
  Mount an approved certificate instead with `--tls-cert` / `--tls-key`.

The plain-HTTP port `5080` is not published by Compose and rejects any non-loopback
request that does not arrive through a trusted proxy with `X-Forwarded-Proto: https`
(HTTP 400 `HTTPS_REQUIRED`), so it cannot serve the app insecurely.

The stack does not claim fiscal, payment-provider, printer or signed go-live readiness.

The Compose project name is fixed to `alkaros`, so rebuilds replace the same service set instead of creating a second
project group when the checkout directory name changes. `--remove-orphans` removes services left by an older ALKAROS
Compose definition; it intentionally does not delete unrelated standalone containers or their data.

## HTTPS and WaiterPwa offline field readiness

The WaiterPwa offline queue relies on a service worker, and browsers only register
a service worker in a secure context: HTTPS with a certificate the device trusts,
or `localhost`. A waiter phone reaching the stack over `http://<lan-ip>:5080`
gets no offline mode, and the app now shows `Çevrimdışı mod kapalı` instead of
failing silently.

Certificate strategy for a LAN deployment (no public domain):

1. Set `ALKAROS_PROXY_HOST` to the host name or LAN IP the devices use before
   `docker compose up`, e.g. `ALKAROS_PROXY_HOST=192.168.1.50`. Caddy's internal
   CA then issues a certificate whose SAN matches that address, and the Host's
   own self-signed fallback certificate (`8444`) gets the same SAN.
2. Export the internal root CA and install it on every waiter device:

   ```sh
   docker compose cp proxy:/data/caddy/pki/authorities/local/root.crt ./alkaros-root.crt
   ```

   Transfer `alkaros-root.crt` to each phone and install it as a trusted CA
   (Android: Settings → Security → Encryption & credentials → Install a certificate
   → CA certificate; iOS: install the profile, then enable full trust under
   Settings → General → About → Certificate Trust Settings).
   For the Host fallback on `8444`, export and install its self-signed leaf too:

   ```sh
   docker compose cp host:/app/tls/. ./host-tls/
   ```

3. For an external go-live, replace `tls internal` with an approved public
   certificate and a real domain, or mount one into Host with `--tls-cert` /
   `--tls-key`.

Field test before go-live:

1. `docker compose up --detach --build --wait` with `ALKAROS_PROXY_HOST` set.
2. On a waiter phone joined to the same network, open `https://<ALKAROS_PROXY_HOST>:8443`
   after installing the root CA; confirm the padlock shows a trusted connection.
3. Sign in, open a table, turn off WiFi, and enter an order. The status ribbon
   must read `Çevrimdışı • İşlemler Güvenli Kuyrukta`, not `Çevrimdışı mod kapalı`.
4. Turn WiFi back on and confirm the queued order reaches the server and the
   ribbon returns to `Çevrimiçi`.
5. Repeat step 2 over plain `http://<lan-ip>:5080` (not published by Compose) and
   confirm the app shows the `Çevrimdışı mod kapalı • Güvenli bağlantı (HTTPS)
   gerekli` warning.
6. Open `https://<ALKAROS_PROXY_HOST>:8444` (Host's own HTTPS) and repeat step 3;
   the offline flow must work there too, proving the app is not dependent on
   Caddy for a secure context.

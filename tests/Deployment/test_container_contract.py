"""Static release-contract checks for the production container surface.

Topology (A1): a Caddy `web` image serves the static client bundles and
terminates TLS, reverse-proxying the JSON API + SignalR hubs to an `api` image
that runs `ALKAROS.Host serve --api-only` on plain HTTP. Operator maintenance
tools live in compose.ops.yaml; local port publishing lives in compose.dev.yaml.
"""

from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[2]
DOCKERFILE = ROOT / "deploy" / "docker" / "Dockerfile"


def test_dockerfile_is_multi_stage_and_uses_pinned_toolchains() -> None:
    dockerfile = DOCKERFILE.read_text(encoding="utf-8")

    assert not (ROOT / "Dockerfile").exists(), "the Dockerfile now lives under deploy/docker/"
    assert "sdk:10.0.302@sha256:" in dockerfile
    assert "node:24.6.0-bookworm-slim@sha256:" in dockerfile
    assert "aspnet:8.0-alpine@sha256:" in dockerfile
    assert "caddy:2.9-alpine@sha256:" in dockerfile
    assert dockerfile.count("FROM ") == 4
    assert "AS backend-build" in dockerfile
    assert "AS frontend-build" in dockerfile
    assert "AS api" in dockerfile
    assert "AS web" in dockerfile
    assert "dotnet restore ALKAROS.slnx --locked-mode" in dockerfile
    assert "dotnet publish src/Host/ALKAROS.Host.csproj --configuration Release --no-restore" in dockerfile
    assert "COPY --from=backend-build /out/api ./" in dockerfile
    assert "COPY --from=frontend-build /out/app /srv/app" in dockerfile


def test_api_image_is_headless_http_only() -> None:
    dockerfile = DOCKERFILE.read_text(encoding="utf-8")
    api_stage = dockerfile.split("AS api", 1)[1].split("AS web", 1)[0]

    assert "EXPOSE 5080" in api_stage
    assert "5443" not in api_stage and "8444" not in api_stage
    assert '"--api-only"' in api_stage
    assert '"--customer-display-origin-header", "X-Alkaros-Origin"' in api_stage
    assert "--web-root" not in api_stage
    assert "--self-signed-host" not in api_stage
    # no static assets in the backend image
    assert "wwwroot" not in api_stage
    assert "/srv/app" not in api_stage


def test_compose_core_has_web_and_api_and_no_published_datastore() -> None:
    compose = (ROOT / "compose.yaml").read_text(encoding="utf-8")

    assert compose.startswith("name: alkaros\n")
    assert re.search(r"postgres:18@sha256:[0-9a-f]{64}", compose)
    assert "\n  api:\n" in compose
    assert "\n  web:\n" in compose
    assert "\n  host:\n" not in compose and "\n  proxy:\n" not in compose
    assert "dockerfile: deploy/docker/Dockerfile" in compose
    assert "target: api" in compose
    assert "target: web" in compose
    assert "alkaros-postgres:/var/lib/postgresql" in compose
    assert "condition: service_healthy" in compose
    assert "condition: service_completed_successfully" in compose
    assert "health/ready" in compose
    assert "restart: unless-stopped" in compose
    assert 'ports:\n      - "8443:443"' in compose
    assert "8444:" not in compose
    assert "alkaros-host-tls" not in compose
    assert '["CMD", "wget", "-q", "--spider", "https://localhost/health/ready"]' in compose
    assert "5432:" not in compose and "5080:" not in compose
    assert "ALKAROS_DB_PASSWORD:" not in compose
    assert compose.count("export ALKAROS_DB_PASSWORD=\"$$(tr -d '\\r\\n' < /run/secrets/db_password)\"") == 3
    assert "${ALKAROS_KITCHEN_STATION_ID:-kitchen-main}" in compose
    assert "--api-only --customer-display-origin-header X-Alkaros-Origin" in compose

    dockerignore = (ROOT / ".dockerignore").read_text(encoding="utf-8")
    assert "deploy/docker/db_password" in dockerignore
    assert "deploy/docker/admin_password" in dockerignore
    deployment_gitignore = (ROOT / "deploy" / "docker" / ".gitignore").read_text(encoding="utf-8")
    assert deployment_gitignore.splitlines() == [
        "db_password",
        "!db_password.example",
        "admin_password",
        "!admin_password.example",
    ]


def test_operator_tools_are_split_into_an_ops_overlay() -> None:
    core = (ROOT / "compose.yaml").read_text(encoding="utf-8")
    ops = (ROOT / "compose.ops.yaml").read_text(encoding="utf-8")

    for service in ("backup:", "basebackup:", "housekeeping:"):
        assert service not in core, f"{service} must not be in the core `up` stack"
        assert service in ops
    assert ops.startswith("name: alkaros\n")
    assert ops.count('profiles: ["ops"]') == 3
    assert "exec dotnet ALKAROS.Host.dll housekeeping --db-url" in ops
    assert "/repo/deploy/docker/backup.sh" in ops
    assert "/repo/deploy/docker/basebackup.sh" in ops


def test_dev_overlay_serves_plain_http_and_publishes_ports() -> None:
    dev = (ROOT / "compose.dev.yaml").read_text(encoding="utf-8")
    caddy_dev = (ROOT / "deploy" / "docker" / "Caddyfile.dev").read_text(encoding="utf-8")

    assert dev.startswith("name: alkaros\n")
    assert '"5433:5432"' in dev          # 5432 is often taken by a standalone test db
    assert '"5080:5080"' in dev
    assert '"8090:80"' in dev            # main origin
    assert '"8091:81"' in dev            # customer-display origin (B-4, no *.localhost DNS)
    assert "ports: !override" in dev      # replace, not append to, the core 8443 mapping
    assert "deploy/docker/Caddyfile.dev:/etc/caddy/Caddyfile" in dev

    assert "auto_https off" in caddy_dev
    assert "header_up X-Forwarded-Proto https" in caddy_dev
    assert "reverse_proxy api:5080" in caddy_dev
    assert "http://:80" in caddy_dev   # match any Host - a phone sends the LAN IP, not "localhost"
    assert "http://:81" in caddy_dev
    assert "header_up X-Alkaros-Origin display" in caddy_dev
    assert "header_up -X-Alkaros-Origin" in caddy_dev
    # plain-HTTP localhost must keep the session cookie
    assert 'header_down Set-Cookie "(?i);\\s*secure" ""' in caddy_dev


def test_services_load_the_password_from_the_mounted_secret() -> None:
    compose = (ROOT / "compose.yaml").read_text(encoding="utf-8")

    assert compose.count('entrypoint: ["/bin/sh", "-eu", "-c"]') == 3
    assert "exec dotnet ALKAROS.Host.dll --order-manifest" in compose
    assert "exec dotnet ALKAROS.Host.dll serve" in compose


def test_manager_provisioning_is_ordered_and_secret_backed() -> None:
    compose = (ROOT / "compose.yaml").read_text(encoding="utf-8")

    assert "provision:" in compose
    assert "migrate: { condition: service_completed_successfully }" in compose
    assert "provision: { condition: service_completed_successfully }" in compose
    assert 'export ALKAROS_BOOTSTRAP_PASSWORD="$$(tr -d \'\\r\\n\' < /run/secrets/admin_password)"' in compose
    assert "exec dotnet ALKAROS.Host.dll provision-manager --db-url" in compose
    assert "ALKAROS_BOOTSTRAP_PASSWORD:" not in compose
    assert "file: ./deploy/docker/admin_password" in compose


def test_caddy_serves_statics_and_isolates_the_customer_display_origin() -> None:
    caddyfile = (ROOT / "deploy" / "docker" / "Caddyfile").read_text(encoding="utf-8")

    assert "tls internal" in caddyfile
    assert "reverse_proxy api:5080" in caddyfile
    assert "reverse_proxy host:5080" not in caddyfile
    assert "root * /srv/app" in caddyfile
    assert "try_files {path} {path}/ /index.html" in caddyfile  # serves /waiter/ + /cashier/ index too
    assert "file_server" in caddyfile
    # B-4: the display vhost tags the origin; the main vhost strips any inbound copy.
    assert "header_up X-Alkaros-Origin display" in caddyfile
    assert "header_up -X-Alkaros-Origin" in caddyfile
    assert "display.{$ALKAROS_PROXY_HOST:localhost}" in caddyfile
    assert not caddyfile.lstrip().startswith(":443")

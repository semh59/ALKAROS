"""Static release-contract checks for the production container surface."""

from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[2]


def test_dockerfile_is_multi_stage_and_uses_pinned_toolchains() -> None:
    dockerfile = (ROOT / "Dockerfile").read_text(encoding="utf-8")

    assert "sdk:10.0.302@sha256:" in dockerfile
    assert "node:24.6.0-bookworm-slim@sha256:" in dockerfile
    assert "aspnet:8.0-alpine@sha256:" in dockerfile
    assert dockerfile.count("FROM ") == 3
    assert "dotnet restore ALKAROS.slnx --locked-mode" in dockerfile
    assert "dotnet publish src/Host/ALKAROS.Host.csproj --configuration Release --no-restore" in dockerfile
    assert "COPY --from=host-build /out/host ./" in dockerfile
    assert "COPY --from=ui-build /src/dist ./wwwroot" in dockerfile


def test_compose_has_ordered_migration_readiness_and_no_database_port() -> None:
    compose = (ROOT / "compose.yaml").read_text(encoding="utf-8")

    assert compose.startswith("name: alkaros\n")
    assert re.search(r"postgres:18@sha256:[0-9a-f]{64}", compose)
    assert "caddy:2.9-alpine@sha256:" in compose
    assert "alkaros-postgres:/var/lib/postgresql" in compose
    assert "condition: service_healthy" in compose
    assert "condition: service_completed_successfully" in compose
    assert "health/ready" in compose
    assert "restart: unless-stopped" in compose
    assert 'ports:\n      - "8443:443"' in compose
    assert '["CMD", "wget", "-q", "--spider", "https://localhost/health/ready"]' in compose
    assert "5432:" not in compose
    assert "db_password" in compose
    assert "ALKAROS_DB_PASSWORD:" not in compose
    assert compose.count("export ALKAROS_DB_PASSWORD=\"$$(tr -d '\\r\\n' < /run/secrets/db_password)\"") == 3
    assert "${ALKAROS_KITCHEN_STATION_ID:-kitchen-main}" in compose
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


def test_caddy_tls_site_has_an_explicit_certificate_identity() -> None:
    caddyfile = (ROOT / "deploy" / "docker" / "Caddyfile").read_text(encoding="utf-8")

    assert caddyfile.startswith("https://localhost {\n")
    assert "tls internal" in caddyfile
    assert "reverse_proxy host:5080" in caddyfile
    assert not caddyfile.startswith(":443")

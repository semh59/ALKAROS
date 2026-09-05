import json
from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[4]
WWWROOT = WORKSPACE / "src" / "Clients" / "WaiterPwa" / "wwwroot"


def test_waiter_pwa_static_files_exist():
    """Verify that all required static assets for Waiter PWA exist."""
    assert (WWWROOT / "index.html").is_file(), "index.html must exist"
    assert (WWWROOT / "manifest.json").is_file(), "manifest.json must exist"
    assert (WWWROOT / "sw.js").is_file(), "sw.js service worker must exist"
    assert (WWWROOT / "waiter-app.css").is_file(), "waiter-app.css must exist"
    assert (WWWROOT / "waiter-app.js").is_file(), "waiter-app.js must exist"


def test_waiter_pwa_manifest_validity():
    """Verify manifest.json is valid and contains standard PWA properties."""
    manifest_path = WWWROOT / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

    assert "name" in manifest
    assert "short_name" in manifest
    assert manifest.get("display") == "standalone"
    assert manifest.get("start_url") == "./index.html"
    assert "theme_color" in manifest
    assert "background_color" in manifest


def test_waiter_pwa_html_landmarks_and_meta():
    """Verify index.html contains mobile viewport, PWA meta, and essential landmarks."""
    html = (WWWROOT / "index.html").read_text(encoding="utf-8")

    assert 'name="viewport"' in html
    assert "viewport-fit=cover" in html
    assert 'rel="manifest"' in html
    assert 'id="tablesGrid"' in html
    assert 'id="zoneList"' in html
    assert 'id="orderDrawer"' in html
    assert 'id="orderModal"' in html
    assert 'id="statusRibbon"' in html
    assert 'id="btnSendKitchen"' in html


def test_waiter_pwa_has_a_staff_sign_in_flow():
    """The PWA gates on a cashier session and offers sign-in / sign-out."""
    html = (WWWROOT / "index.html").read_text(encoding="utf-8")
    app_code = (WWWROOT / "waiter-app.js").read_text(encoding="utf-8")

    assert 'id="loginForm"' in html
    assert 'id="loginUsername"' in html
    assert 'id="loginPassword"' in html

    # Signs in against the shared auth endpoint with this device's own terminal id.
    assert "/api/v1/auth/login" in app_code
    assert "alkaros_waiter_terminal_id" in app_code
    # crypto.randomUUID() is secure-context only; a LAN-IP phone is plain HTTP,
    # so there is a getRandomValues() fallback and every id goes through it.
    assert "function randomUUID(" in app_code
    assert "crypto.getRandomValues" in app_code
    assert "id = randomUUID()" in app_code
    # Gates before loading, and re-prompts on a 401.
    assert "hasValidSession" in app_code
    assert "showLogin" in app_code
    assert "/api/v1/auth/logout" in app_code


def test_waiter_pwa_service_worker_lifecycle():
    """Verify sw.js registers standard caching and fetch lifecycle events."""
    sw_code = (WWWROOT / "sw.js").read_text(encoding="utf-8")

    assert "addEventListener('install'" in sw_code or 'addEventListener("install"' in sw_code
    assert "addEventListener('activate'" in sw_code or 'addEventListener("activate"' in sw_code
    assert "addEventListener('fetch'" in sw_code or 'addEventListener("fetch"' in sw_code
    assert "caches.open" in sw_code


def test_waiter_pwa_real_api_and_reliable_queue():
    """Verify waiter-app.js connects to Host API and keeps items in queue when server call fails."""
    app_code = (WWWROOT / "waiter-app.js").read_text(encoding="utf-8")

    # Authoritative Host Endpoints
    assert "/orders/table-draft" in app_code
    assert "/table-management/zones" in app_code
    assert "/table-management/tables" in app_code
    assert "/catalog-management/categories" in app_code

    # Reliable queue: checks response.ok and only deletes on success
    assert "response.ok" in app_code
    assert "flushOfflineQueue" in app_code
    assert "queueOrderAction" in app_code
    assert "mock-session-token" not in app_code

    # Bağımsız denetimde bulundu (2026-09-05): btnSendKitchen'in click
    # handler'ı async'ti ama yeniden girişe (çift tıklama, ya da istek
    # sürerken ikinci bir tıklama) karşı korunmuyordu.
    assert "dispatchInFlight" in app_code
    assert "btnSendKitchen.disabled = true" in app_code

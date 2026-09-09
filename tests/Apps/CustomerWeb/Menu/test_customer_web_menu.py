"""V12-CWB-001: static-asset checks for the QR customer menu page.

Mirrors tests/Clients/Cashier/Frontend/test_cashier_frontend.py's own
convention for a vanilla (no bundler) HTML/CSS/JS client shell — regex/string
assertions against the shipped files rather than executing the JS, since
this page has no build step or test runner of its own.
"""

from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[4]
WWWROOT = WORKSPACE / "src" / "Apps" / "CustomerWeb" / "Menu" / "wwwroot"


def test_customer_web_menu_static_files_exist():
    assert (WWWROOT / "index.html").is_file(), "index.html must exist"
    assert (WWWROOT / "menu-app.css").is_file(), "menu-app.css must exist"
    assert (WWWROOT / "menu-app.js").is_file(), "menu-app.js must exist"


def test_customer_web_menu_html_structure():
    html = (WWWROOT / "index.html").read_text(encoding="utf-8")

    assert 'id="categoryTabs"' in html
    assert 'id="productList"' in html
    assert 'id="loadingState"' in html
    assert 'id="errorState"' in html
    assert 'id="emptyState"' in html
    assert '<script src="./menu-app.js">' in html
    assert 'lang="tr"' in html


def test_customer_web_menu_javascript_session_and_menu_flow():
    app_code = (WWWROOT / "menu-app.js").read_text(encoding="utf-8")

    # V12-QRS-002/003: the raw table token is exchanged for a session
    # exactly once; every other call uses the session header, never the raw
    # token again.
    assert "issueSession" in app_code
    assert "/api/v1/qr/sessions" in app_code
    assert "/api/v1/qr/menu" in app_code
    assert "X-Alkaros-Qr-Session" in app_code

    # Stale-session handling (CWB-001's own "eski oturum yönetimi" scope
    # item): a 401 from /menu re-issues a session once before giving up,
    # rather than leaving the customer stuck on a dead session silently.
    assert "loadMenuWithSessionRetry" in app_code

    # docs/UI_STYLE_GUIDE.md: the backend's own Turkish error.message is
    # shown as-is; the page must never fall back to a raw HTTP status or an
    # English string of its own invention when that message is available.
    assert "GENERIC_ERROR_MESSAGE" in app_code
    assert "message" in app_code


def test_customer_web_menu_cart_accumulates_but_never_submits():
    """CWB-001's own Out of scope is cart SUBMISSION (sending the order),
    payment, QR token issuance and menu management — not cart accumulation
    itself: a "Sepete ekle" button and a real Turkish "Sepetim" cart bar are
    in scope (V12-CWB-002 adds OrderEntry's own page, linked from here, which
    is what actually calls /api/v1/qr/orders)."""
    app_code = (WWWROOT / "menu-app.js").read_text(encoding="utf-8")
    html = (WWWROOT / "index.html").read_text(encoding="utf-8")

    assert "/api/v1/qr/orders" not in app_code
    assert "CartStore" in app_code
    assert "alkaros.qr.cart" in app_code
    assert "Sepete ekle" in html or "Sepete ekle" in app_code
    assert 'href="./order-entry.html"' in html

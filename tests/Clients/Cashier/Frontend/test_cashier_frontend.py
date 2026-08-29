import json
from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[4]
WWWROOT = WORKSPACE / "src" / "Clients" / "Cashier" / "wwwroot"


def test_cashier_static_files_exist():
    """Verify that all required static assets for Cashier Quick POS exist."""
    assert (WWWROOT / "index.html").is_file(), "index.html must exist"
    assert (WWWROOT / "manifest.json").is_file(), "manifest.json must exist"
    assert (WWWROOT / "cashier-app.css").is_file(), "cashier-app.css must exist"
    assert (WWWROOT / "cashier-app.js").is_file(), "cashier-app.js must exist"


def test_cashier_manifest_validity():
    """Verify manifest.json is valid and contains standard POS PWA properties."""
    manifest_path = WWWROOT / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

    assert "name" in manifest
    assert "short_name" in manifest
    assert manifest.get("display") == "standalone"
    assert manifest.get("start_url") == "./index.html"
    assert "theme_color" in manifest
    assert "background_color" in manifest


def test_cashier_html_structure():
    """Verify index.html contains split layout, search input, ticket stream, and quick cash buttons."""
    html = (WWWROOT / "index.html").read_text(encoding="utf-8")

    assert 'id="searchInput"' in html
    assert 'id="categoryTabs"' in html
    assert 'id="productMatrix"' in html
    assert 'id="ticketItemsStream"' in html
    assert 'id="grandTotalAmount"' in html
    assert 'id="btnPayCash"' in html
    assert 'id="btnParkTicket"' in html
    assert 'id="btnRecallTicket"' in html
    assert 'id="changeModal"' in html
    assert 'id="changeDueText"' in html


def test_cashier_javascript_pos_engine():
    """Verify cashier-app.js implements ticket calculations, quick cash chips, and park/recall logic."""
    app_code = (WWWROOT / "cashier-app.js").read_text(encoding="utf-8")

    assert "ticketItems" in app_code
    assert "addProductToTicket" in app_code
    assert "completeCashSale" in app_code
    assert "parkCurrentTicket" in app_code
    assert "recallParkedTicket" in app_code
    assert "isComplimentary" in app_code
    assert "formatMoney" in app_code

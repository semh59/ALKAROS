from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[4]
WWWROOT = WORKSPACE / "src" / "Clients" / "Cashier" / "wwwroot"


def test_product_grid_renders_remaining_count_badge():
    """V1-CUI-010: the Kasa product grid shows the same "Kalan N" badge
    Garson's util.js renders (V1-WTR-056) for the shared /catalog
    endpoint's remainingCount. Cashier and WaiterPwa are separate apps
    with no shared JS module, so this is its own implementation - same
    class names and wording on purpose, not copy-pasted markup drift."""
    app_js = (WWWROOT / "cashier-app.js").read_text(encoding="utf-8")
    css = (WWWROOT / "cashier-app.css").read_text(encoding="utf-8")

    assert "renderStockBadge(prod.remainingCount)" in app_js
    assert "remainingCount: p.remainingCount" in app_js
    assert "product-stock" in css
    assert "is-low" in css

    # V1-WTR-054 already drops a zero/negative-remaining product from the
    # shared catalog response entirely - this screen must not reinvent a
    # disabled/"Tükendi" card state for a case that never reaches it.
    assert "Tükendi" not in app_js

from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[4]
WWWROOT = WORKSPACE / "src" / "Clients" / "WaiterPwa" / "wwwroot"


def test_menu_screen_renders_remaining_count_badge():
    """V1-WTR-055/056: menu.js shows the same low-stock badge bill.js
    already renders for a sent line (V1-RMD-143), via the shared
    renderStockBadge helper (V1-WTR-056) — reused here for a product still
    being chosen. remainingCount null renders nothing — the catalog's own
    stock-tracked/untracked distinction, not a client guess."""
    menu_js = (WWWROOT / "js" / "screens" / "menu.js").read_text(encoding="utf-8")
    util_js = (WWWROOT / "js" / "util.js").read_text(encoding="utf-8")

    assert "renderStockBadge(product.remainingCount)" in menu_js
    assert "from '../util.js'" in menu_js

    # The badge's own markup/threshold lives in ONE place (V1-WTR-056), not
    # duplicated per caller.
    assert "product-stock" in util_js
    assert "is-low" in util_js
    assert "Kalan" in util_js

    # V1-WTR-054 already drops a zero/negative-remaining product from the
    # catalog response entirely — menu.js must not reinvent a
    # disabled/"Tükendi" card state for a case that never reaches it.
    assert "Tükendi" not in menu_js


def test_bill_screen_shares_the_same_stock_badge_helper():
    """V1-WTR-056: bill.js's already-sent-line badge (V1-RMD-143) must call
    the same helper as menu.js, not keep its own independent copy."""
    bill_js = (WWWROOT / "js" / "sheets" / "bill.js").read_text(encoding="utf-8")

    assert "renderStockBadge(item.availableStockQuantity, { allowOut: true })" in bill_js


def test_catalog_load_maps_remaining_count_into_state():
    """waiter-app.js's loadCatalog() must carry the server's remainingCount
    into state.products, or menu.js's badge would always render null."""
    app_js = (WWWROOT / "waiter-app.js").read_text(encoding="utf-8")

    assert "remainingCount: product.remainingCount" in app_js

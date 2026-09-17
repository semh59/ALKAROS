from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[4]
WWWROOT = WORKSPACE / "src" / "Clients" / "Cashier" / "wwwroot"


def test_load_catalog_maps_real_dto_field_names():
    """V1-RMD-227: loadCatalog() must read the real CatalogProductDto field
    names (unitPrice, categoryCode) - not currentPrice/categoryId, which
    never existed on the response and left every product at ₺0,00 in an
    always-'uncategorized' bucket (the same defect class V1-RMD-129 fixed
    for waiter-app.js, unaudited here until now)."""
    app_js = (WWWROOT / "cashier-app.js").read_text(encoding="utf-8")

    assert "Number(p.unitPrice ?? 0)" in app_js
    assert "p.categoryCode || 'uncategorized'" in app_js

    # The two field names that never existed on the real response must not
    # come back - a regression here silently zeroes every price again.
    assert "currentPrice" not in app_js
    assert "p.categoryId ||" not in app_js

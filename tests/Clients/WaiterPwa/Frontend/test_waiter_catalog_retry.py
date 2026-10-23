from pathlib import Path

WWWROOT = Path(__file__).resolve().parents[4] / "src" / "Clients" / "WaiterPwa" / "wwwroot"


def test_first_load_failure_is_flagged_and_cleared_by_a_successful_load():
    app = (WWWROOT / "waiter-app.js").read_text(encoding="utf-8")

    assert "state.catalogFailed = true;" in app
    assert "state.catalogFailed = false;" in app
    assert "catalogFailed: false" in (WWWROOT / "js" / "state.js").read_text(encoding="utf-8")


def test_failed_menu_offers_retry_and_an_empty_menu_says_so():
    menu = (WWWROOT / "js" / "screens" / "menu.js").read_text(encoding="utf-8")
    app = (WWWROOT / "waiter-app.js").read_text(encoding="utf-8")

    assert "state.catalogFailed" in menu
    assert "Menü alınamadı." in menu
    assert "data-retry-catalog" in menu
    assert "Menüde ürün yok." in menu
    assert "event.target.closest('[data-retry-catalog]')" in app

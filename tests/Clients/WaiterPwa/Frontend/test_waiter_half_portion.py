from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[4]
WWWROOT = WORKSPACE / "src" / "Clients" / "WaiterPwa" / "wwwroot"


def read(*parts):
    return (WWWROOT.joinpath(*parts)).read_text(encoding="utf-8")


def test_catalog_load_reads_the_half_portion_flag_and_defaults_to_false():
    app = read("waiter-app.js")

    assert "allowsHalfPortion: product.allowsHalfPortion === true" in app


def test_half_button_is_rendered_only_for_a_flagged_product():
    menu = read("js", "screens", "menu.js")

    assert "${product.allowsHalfPortion ? `<button" in menu
    assert menu.count('class="product-half"') == 1


def test_product_sheet_offers_half_quantities_only_for_a_flagged_product():
    sheet = read("js", "sheets", "product-sheet.js")

    assert "context.product.allowsHalfPortion ? [0.5, 1, 1.5, 2, 3] : [1, 2, 3, 4, 5]" in sheet
    assert "yarım porsiyon 0,5" in sheet
    assert "const quantities = [0.5" not in sheet


def test_draft_line_steps_by_a_whole_unit_unless_its_product_allows_halves():
    app = read("waiter-app.js")
    menu = read("js", "screens", "menu.js")

    assert "allowsHalfPortion: product.allowsHalfPortion === true," in menu
    assert "? line.quantity + (up ? 0.5 : -0.5)" in app
    assert ": (up ? Math.floor(line.quantity) + 1 : Math.ceil(line.quantity) - 1);" in app
    assert "removed.quantity = removed.allowsHalfPortion ? 0.5 : 1;" in app

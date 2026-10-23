from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[4]
APP_JS = WORKSPACE / "src" / "Clients" / "Cashier" / "wwwroot" / "cashier-app.js"


def _matrix_filter_source() -> str:
    text = APP_JS.read_text(encoding="utf-8")
    start = text.index("const query = searchQuery.trim()")
    end = text.index("el.productMatrix.innerHTML = filtered.map")
    return text[start:end]


def test_product_search_lowercases_with_turkish_rules():
    """A product named with a dotted or dotless capital I (İskender) must be
    found by its lowercase typing; plain toLowerCase() maps İ to i + a
    combining dot and never matches."""
    source = _matrix_filter_source()

    assert "toLowerCase()" not in source
    assert source.count("toLocaleLowerCase('tr-TR')") == 3


def test_empty_filter_result_shows_a_message_instead_of_a_blank_grid():
    source = _matrix_filter_source()

    assert "filtered.length === 0" in source
    assert "Aramanıza uyan ürün yok." in source

from pathlib import Path

WWWROOT = Path(__file__).resolve().parents[4] / "src" / "Clients" / "WaiterPwa" / "wwwroot"


def read(*parts):
    return WWWROOT.joinpath(*parts).read_text(encoding="utf-8")


def test_table_age_reads_the_field_the_server_sends():
    tables = read("js", "screens", "tables.js")

    assert "openedAt: table.occupiedSince || null," in tables
    assert "currentOrderOpenedAt" not in tables


def test_a_failed_table_list_is_not_an_empty_restaurant():
    tables = read("js", "screens", "tables.js")

    assert "state.tablesFailed = !result.ok;" in tables
    assert "Masalar alınamadı." in tables
    assert "data-retry-tables" in tables
    assert "Masalar güncellenemedi; son bilinen durum gösteriliyor." in tables
    assert "tablesFailed: false" in read("js", "state.js")
    assert "event.target.closest('[data-retry-tables]')" in read("waiter-app.js")


def test_the_table_list_is_polled_while_it_is_on_screen():
    app = read("waiter-app.js")

    assert "el.tablesScreen.dataset.state === 'on'" in app
    assert "void loadTables();" in app
    assert "}, 15000);" in app


def test_a_transfer_conflict_refreshes_the_tables_and_the_open_table():
    transfer = read("js", "sheets", "transfer.js")

    assert "result.status === 409" in transfer
    assert "if (current) state.table = current;" in transfer

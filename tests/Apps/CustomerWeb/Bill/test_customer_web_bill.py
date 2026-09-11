"""V1-WTR-018: static-asset checks for the QR guest's read-only live bill
page ("misafir için salt-okunur canlı adisyon"). Same convention as
tests/Apps/CustomerWeb/OrderEntry/test_customer_web_order_entry.py.
"""

from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[4]
WWWROOT = WORKSPACE / "src" / "Apps" / "CustomerWeb" / "Bill" / "wwwroot"


def test_customer_web_bill_static_files_exist():
    assert (WWWROOT / "bill.html").is_file()
    assert (WWWROOT / "bill.css").is_file()
    assert (WWWROOT / "bill.js").is_file()


def test_customer_web_bill_html_structure():
    html = (WWWROOT / "bill.html").read_text(encoding="utf-8")

    assert 'id="billList"' in html
    assert 'id="billSubtotal"' in html
    assert 'id="billTax"' in html
    assert 'id="billTotal"' in html
    assert 'id="emptyBillState"' in html
    assert 'id="errorState"' in html
    assert 'lang="tr"' in html
    assert "Adisyonum" in html


def test_customer_web_bill_javascript_is_read_only_and_polls():
    app_code = (WWWROOT / "bill.js").read_text(encoding="utf-8")

    # Shares the exact session contract Menu's own page (menu-app.js) and
    # OrderEntry's own page (order-entry.js) use.
    assert "alkaros.qr.sessionToken" in app_code
    assert "alkaros.qr.tableToken" in app_code
    assert "/api/v1/qr/bill" in app_code
    assert "X-Alkaros-Qr-Session" in app_code

    # Live: this page polls on its own, a guest never has to refresh by hand.
    assert "POLL_INTERVAL_MS" in app_code
    assert "setInterval" in app_code

    # A dropped poll must not stop the page from trying again.
    assert "A dropped/erroring poll does not stop the next one" in app_code


def test_customer_web_bill_never_calls_a_write_route():
    """This is a read-only screen - the whole point of the idea (garson-
    karsilastirma #6) is that a guest can watch their own tab without being
    able to change it. No cart, no submission, no accept/reject."""
    app_code = (WWWROOT / "bill.js").read_text(encoding="utf-8")

    assert "/api/v1/qr/orders" not in app_code
    assert "/api/v1/qr/sessions" in app_code  # session bootstrap only, not a mutation
    assert "/accept" not in app_code
    assert "/reject" not in app_code
    assert "payment" not in app_code.lower()


def test_customer_web_bill_handles_the_empty_and_error_states_explicitly():
    """hasActiveOrder is false whenever there is nothing to show yet (no
    order placed, or the check already closed) - the page must branch on it
    rather than assuming lines is always populated."""
    app_code = (WWWROOT / "bill.js").read_text(encoding="utf-8")

    assert "hasActiveOrder" in app_code
    assert "GENERIC_ERROR_MESSAGE" in app_code
    assert "QR kodunu tekrar okutun" in app_code

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


def test_customer_web_bill_renders_the_business_own_identity():
    """V1-SET-009: same business-identity brand block as Menu's/OrderEntry's
    own pages (V1-SET-007/008) - hidden by default."""
    html = (WWWROOT / "bill.html").read_text(encoding="utf-8")
    css = (WWWROOT / "bill.css").read_text(encoding="utf-8")
    app_code = (WWWROOT / "bill.js").read_text(encoding="utf-8")

    assert 'id="businessBrand" class="business-brand" hidden' in html
    assert 'id="businessLogo"' in html
    assert 'id="businessName"' in html
    assert ".business-brand" in css
    assert ".business-logo" in css

    assert "loadBranding" in app_code
    assert "/api/v1/qr/branding" in app_code
    assert "/api/v1/qr/logo" in app_code
    assert "accentColor" in app_code
    assert "businessName" in app_code
    assert "hasLogo" in app_code
    assert "--cw-accent" in app_code


def test_customer_web_bill_does_not_let_a_transient_poll_failure_stick_forever():
    """V1-RMD-392 (Tur 2, P2): showError() itself never hid errorState again,
    and nothing else did either - a guest whose very first poll failed (a
    genuine one-off network blip, exactly what startPolling()'s own comment
    already says must not "permanently freeze this page") kept seeing a
    role="alert" error banner forever, even once later polls started
    rendering a correct, live-updating bill right next to it. renderBill()
    must clear the error state on every successful render, and pollOnce()
    must stop re-showing the error at all once at least one render has
    actually happened - a guest's own bill staying on screen through a blip
    is what the header's "canlı güncellenir" promise is actually for."""
    app_code = (WWWROOT / "bill.js").read_text(encoding="utf-8")

    assert "hasRenderedOnce" in app_code

    render_bill_start = app_code.index("function renderBill(")
    render_bill_body = app_code[render_bill_start:app_code.index("\n}\n", render_bill_start)]
    assert '"errorState").hidden = true' in render_bill_body

    poll_once_start = app_code.index("async function pollOnce(")
    poll_once_body = app_code[poll_once_start:app_code.index("\n}\n", poll_once_start)]
    assert "hasRenderedOnce = true" in poll_once_body
    assert "if (!hasRenderedOnce)" in poll_once_body


def test_customer_web_bill_live_regions_are_actually_announced():
    """V1-RMD-375 (module-by-module UI audit, 2026-09-27): the header's own
    copy promises "Bu ekran canlı güncellenir" (updates live - bill.js polls
    every few seconds), but neither the item list nor the running total had
    any aria-live at all - a guest using a screen reader had no way to know
    a new item or price change had landed. aria-atomic="false" on the list
    matches Cashier's own ticket-lines precedent (Cashier.tsx) so only what
    actually changed is read, not the whole bill re-announced on every poll."""
    html = (WWWROOT / "bill.html").read_text(encoding="utf-8")

    assert "Bu ekran canlı güncellenir" in html
    assert 'id="billList" class="bill-list" aria-live="polite" aria-atomic="false"' in html
    assert 'id="billSummary" class="bill-summary" role="status" aria-live="polite"' in html

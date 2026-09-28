"""V12-CWB-002: static-asset checks for the QR order-entry (cart) page.

Same convention as tests/Apps/CustomerWeb/Menu/test_customer_web_menu.py.
"""

from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[4]
WWWROOT = WORKSPACE / "src" / "Apps" / "CustomerWeb" / "OrderEntry" / "wwwroot"


def test_customer_web_order_entry_static_files_exist():
    assert (WWWROOT / "order-entry.html").is_file()
    assert (WWWROOT / "order-entry.css").is_file()
    assert (WWWROOT / "order-entry.js").is_file()


def test_customer_web_order_entry_html_structure():
    html = (WWWROOT / "order-entry.html").read_text(encoding="utf-8")

    assert 'id="cartList"' in html
    assert 'id="cartGrandTotal"' in html
    assert 'id="btnSubmitOrder"' in html
    assert 'id="orderStatus"' in html
    assert 'id="emptyCartState"' in html
    assert 'lang="tr"' in html
    assert "Siparişi Gönder" in html


def test_customer_web_order_entry_javascript_submission_flow():
    app_code = (WWWROOT / "order-entry.js").read_text(encoding="utf-8")

    # Shares the exact cart contract Menu's own CartStore writes.
    assert "alkaros.qr.cart" in app_code
    assert "/api/v1/qr/orders" in app_code
    assert "X-Alkaros-Qr-Session" in app_code

    # Duplicate-submission protection (CWB-002's own "yinelenen gönderim
    # koruması" scope item): a persisted submission id survives a reload
    # mid-submit instead of generating a new one on every attempt.
    assert "alkaros.qr.submissionId" in app_code
    assert "readOrCreateSubmissionId" in app_code

    # Pending-order status polling (CWB-002's own "beklemede-order status"
    # scope item) — the Order is materialized asynchronously, never assumed
    # to exist right after the 202.
    assert "pollUntilMaterialized" in app_code
    assert "Pending" in app_code
    # Found via real end-to-end Docker verification (2026-09-09): the walk
    # from queued to PendingConfirmation passes through Draft/Submitted for
    # a brief moment (Order's own QrOrderSubmittedConsumer) - polling must
    # not stop there, only "Pending" (no orders.orders row at all yet) and
    # a real terminal status should end the loop.
    assert "isStillMaterializing" in app_code
    assert '"Draft"' in app_code
    assert '"Submitted"' in app_code

    # Double-click guard, same shape as Cashier's own dispatchInFlight.
    assert "submissionInFlight" in app_code


def test_customer_web_order_entry_out_of_scope_features_are_not_faked():
    """CWB-002's own Out of scope: doğrudan mutfağa gönderim (direct kitchen
    dispatch), müşteri payment, personel onayı (staff confirmation — that is
    PendingOrderConfirmationStore's own Accept/Reject HTTP surface, a
    manager-facing screen this page never calls) and menü yönetimi."""
    app_code = (WWWROOT / "order-entry.js").read_text(encoding="utf-8")

    assert "/accept" not in app_code
    assert "/reject" not in app_code
    assert "payment" not in app_code.lower()
    assert "kitchenTicket" not in app_code


def test_customer_web_order_entry_renders_the_business_own_identity():
    """V1-SET-009: same business-identity brand block as Menu's own page
    (V1-SET-007/008) - hidden by default."""
    html = (WWWROOT / "order-entry.html").read_text(encoding="utf-8")
    css = (WWWROOT / "order-entry.css").read_text(encoding="utf-8")
    app_code = (WWWROOT / "order-entry.js").read_text(encoding="utf-8")

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


def test_customer_web_order_entry_keeps_watching_after_the_fast_poll_window():
    """V1-RMD-391 (Tur 2, P2): before this, pollUntilMaterialized() gave up for
    good after MAX_POLL_ATTEMPTS (60s) and froze #orderStatusMessage on "biraz
    uzun sürüyor" forever, even though the order kept materializing
    server-side (the comment right above it already said so). It must now
    keep polling at a slower cadence, and only give up with an actionable
    message (reload or call staff) after a much longer window."""
    app_code = (WWWROOT / "order-entry.js").read_text(encoding="utf-8")

    assert "SLOW_POLL_INTERVAL_MS" in app_code
    assert "MAX_SLOW_POLL_ATTEMPTS" in app_code
    # The slow-phase loop must call pollOrderOnce again, not just wait -
    # otherwise the interval constant would exist without ever being used to
    # actually keep checking.
    slow_phase_start = app_code.index("MAX_SLOW_POLL_ATTEMPTS", app_code.index("async function pollUntilMaterialized"))
    slow_phase_body = app_code[slow_phase_start:]
    assert slow_phase_body.count("pollOrderOnce(submissionId)") >= 1
    assert "Sayfayı yenileyin" in app_code or "sayfayı yenileyin" in app_code


def test_customer_web_order_entry_status_section_is_announced_live():
    """V1-RMD-375 (module-by-module UI audit, 2026-09-27): #orderStatusMessage's
    text changes live while pollUntilMaterialized() polls (submitted ->
    accepted/rejected) with no page reload - without role="status" +
    aria-live, a screen reader user got no announcement at all as their
    order's outcome changed."""
    html = (WWWROOT / "order-entry.html").read_text(encoding="utf-8")

    assert 'id="orderStatus" class="order-status" role="status" aria-live="polite"' in html

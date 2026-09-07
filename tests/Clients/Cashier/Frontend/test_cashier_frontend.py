import json
from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[4]
WWWROOT = WORKSPACE / "src" / "Clients" / "Cashier" / "wwwroot"


def test_cashier_static_files_exist():
    """Verify that all required static assets for Cashier Quick POS exist."""
    assert (WWWROOT / "index.html").is_file(), "index.html must exist"
    assert (WWWROOT / "manifest.json").is_file(), "manifest.json must exist"
    assert (WWWROOT / "cashier-app.css").is_file(), "cashier-app.css must exist"
    assert (WWWROOT / "cashier-app.js").is_file(), "cashier-app.js must exist"


def test_cashier_manifest_validity():
    """Verify manifest.json is valid and contains standard POS PWA properties."""
    manifest_path = WWWROOT / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

    assert "name" in manifest
    assert "short_name" in manifest
    assert manifest.get("display") == "standalone"
    assert manifest.get("start_url") == "./index.html"
    assert "theme_color" in manifest
    assert "background_color" in manifest


def test_cashier_html_structure_and_no_fake_payment():
    """Verify index.html contains split layout, search input, ticket stream, and NO fake payment claims."""
    html = (WWWROOT / "index.html").read_text(encoding="utf-8")

    assert 'id="searchInput"' in html
    assert 'id="categoryTabs"' in html
    assert 'id="productMatrix"' in html
    assert 'id="ticketItemsStream"' in html
    assert 'id="grandTotalAmount"' in html
    assert 'id="btnDispatchOrder"' in html
    assert 'id="btnParkTicket"' in html
    assert 'id="btnRecallTicket"' in html

    # V1 contract: No fake payment / tahsilat claims
    assert "Tahsilat Başarılı" not in html
    assert "Nakit Tahsilat & Fiş Kes" not in html
    assert "changeModal" not in html


def test_cashier_javascript_order_engine():
    """Verify cashier-app.js implements ticket calculations, park/recall logic, and dispatch without fake payment."""
    app_code = (WWWROOT / "cashier-app.js").read_text(encoding="utf-8")

    assert "ticketItems" in app_code
    assert "addProductToTicket" in app_code
    assert "dispatchOrderToKitchen" in app_code
    assert "parkCurrentTicket" in app_code
    assert "recallParkedTicket" in app_code
    assert "formatMoney" in app_code

    # V1 contract: No fake cash sale calculations
    assert "completeCashSale" not in app_code
    assert "changeDue" not in app_code

    # Bağımsız denetimde bulundu (2026-09-05): isComplimentary bir istemci-
    # taraflı gösterim numarasıydı — ekranda ₺0 gösteriyordu ama sunucu
    # (OrderManagementStore) kalıcı fiyatı her zaman katalogdan hesapladığı
    # için müşteri gerçekte tam fiyattan faturalanıyordu. Kaldırıldı; gerçek
    # yetkilendirilmiş ikram akışı zaten var (bills.comp grant'i, ayrı bir
    # uç nokta) ve bu ekranın tek-seferlik sipariş oluşturma modeliyle
    # uyumlu değil, ileride ayrı bir görevle bağlanabilir.
    assert "isComplimentary" not in app_code

    # Bağımsız denetimde bulundu (2026-09-05): dispatchOrderToKitchen'de
    # çift tıklamaya (veya yavaş bir istek sürerken ikinci bir tıklamaya)
    # karşı hiçbir koruma yoktu; her tıklama sepeti sunucuya ayrı bir
    # siparişmiş gibi gönderirdi. Sunucu X-Idempotency-Key'i kabul eder
    # ama zorunlu kılmaz, bu yüzden istemci tarafı koruma gerekliydi.
    assert "dispatchInFlight" in app_code
    assert "btnDispatchOrder.disabled = true" in app_code

    # Bağımsız denetimde bulundu (2026-09-07): "Çevrimiçi" rozeti tamamen
    # ölü markup'tı — navigator.onLine kontrolü veya online/offline
    # dinleyicisi hiç yoktu, LAN kesintisinde bile hep "Çevrimiçi" gösterirdi.
    assert "navigator.onLine" in app_code
    assert "updateConnectivityBadge" in app_code
    assert "addEventListener('online'" in app_code
    assert "addEventListener('offline'" in app_code

"""Live-stack walk: login -> draft -> submit -> send-to-cashier -> bill -> pair display -> show-on-display -> partial cash -> display snapshot.
Reads the admin password from deploy/docker/admin_password and never prints it."""
import json, sys, uuid, urllib.request, urllib.error, http.cookiejar

CASHIER = "http://localhost:8090"
DISPLAY = "http://localhost:8091"
TERMINAL = sys.argv[4] if len(sys.argv) > 4 else "d60b3df9-6396-447e-81ba-7babfcf0e205"
KASA = "00000000-0000-0000-0000-000000000001"
SKU = sys.argv[1] if len(sys.argv) > 1 else "FOOD-002"
QTY = int(sys.argv[2]) if len(sys.argv) > 2 else 1
PARTIAL = float(sys.argv[3]) if len(sys.argv) > 3 else 100


class Client:
    def __init__(self, base):
        self.base = base
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar))

    def call(self, method, path, body=None):
        data = None if body is None else json.dumps(body).encode()
        req = urllib.request.Request(self.base + path, data=data, method=method, headers={"Content-Type": "application/json"})
        try:
            with self.opener.open(req) as r:
                raw = r.read().decode()
                return r.status, (json.loads(raw) if raw else None)
        except urllib.error.HTTPError as e:
            raw = e.read().decode()
            try:
                return e.code, json.loads(raw)
            except ValueError:
                return e.code, raw


def step(name, result):
    status, body = result
    shown = body if not isinstance(body, (dict, list)) else json.dumps(body, ensure_ascii=False)[:400]
    print(f"{name}: {status} {shown}")
    return status, body


cashier, display = Client(CASHIER), Client(DISPLAY)
password = open("deploy/docker/admin_password", encoding="utf-8").read().strip()
s, _ = step("login", cashier.call("POST", "/api/v1/auth/login", {"Username": "admin", "Password": password, "TerminalId": TERMINAL}))
assert s == 200
t = f"/api/v1/terminals/{TERMINAL}"

s, catalog = cashier.call("GET", f"{t}/catalog")
products = catalog["products"] if isinstance(catalog, dict) else catalog
product = next(p for p in products if p.get("sku") == SKU)
print("product:", product["name"], product["unitPrice"])

order_id, line_id = str(uuid.uuid4()), str(uuid.uuid4())
payload = {
    "id": order_id, "tableId": KASA, "tableNumber": "KASA-1",
    "items": [{"id": line_id, "productId": product["productId"], "name": product["name"], "productName": product["name"],
               "quantity": QTY, "unitPrice": product["unitPrice"], "specialInstructions": None, "modifiers": []}],
}
s, draft = step("table-draft", cashier.call("POST", f"{t}/orders/table-draft", payload))
assert s == 200
s, _ = step("submit-draft", cashier.call("POST", f"{t}/orders/{draft['orderId']}/submit-draft",
            {"orderId": draft["orderId"], "expectedRowVersion": draft["rowVersion"], "operationId": f"{draft['orderId']}:submit"}))
assert s == 200
s, _ = step("send-to-cashier", cashier.call("POST", f"{t}/orders/{draft['orderId']}/send-to-cashier", {"tableId": KASA}))
assert s == 200
s, bill = step("bill from-order", cashier.call("POST", f"{t}/billing/bills/from-order/{draft['orderId']}"))
assert s == 200
bill_id = bill.get("billId") or bill.get("id")
payable = bill.get("payableAmount") or bill.get("totalAmount")
print("billId:", bill_id, "payable:", payable)

# Pair a display on the display origin, approve from the cashier.
display_id = str(uuid.uuid4())
s, pairing = step("display pairing request", display.call("POST", "/api/v1/customer-displays/pairing-requests", {"DisplayId": display_id}))
assert s == 201
s, _ = step("approve", cashier.call("POST", f"{t}/pairings/approve", {"Code": pairing["code"]}))
assert s == 204
s, _ = step("complete", display.call("POST", f"/api/v1/customer-displays/pairing-requests/{pairing['requestId']}/complete", {"Secret": pairing["secret"]}))
assert s == 200
s, before = step("display snapshot before opening the bill", display.call("GET", f"/api/v1/customer-displays/{display_id}/snapshot"))

s, bound = step("show-on-display", cashier.call("POST", f"{t}/billing/bills/{bill_id}/show-on-display", {}))
assert s == 200 and bound["bound"] is True
s, shown = step("display snapshot after opening the bill", display.call("GET", f"/api/v1/customer-displays/{display_id}/snapshot"))
assert shown["orderId"] == draft["orderId"] and shown["remainingTotal"] == payable and shown["paidTotal"] == 0

s, active = cashier.call("GET", f"{t}/cash-sessions/active")
if s == 404:
    s, active = step("open cash session", cashier.call("POST", f"{t}/cash-sessions/", {"OpeningBalance": 0}))
    assert s == 201
session_id = active.get("cashSessionId")
s, _ = step("partial cash-tender", cashier.call("POST", f"{t}/cash-sessions/{session_id}/cash-tender",
            {"BillId": bill_id, "AmountDue": PARTIAL, "TenderedAmount": PARTIAL, "IdempotencyKey": str(uuid.uuid4())}))
assert s == 200
s, after = step("display snapshot after the partial payment", display.call("GET", f"/api/v1/customer-displays/{display_id}/snapshot"))
assert after["paidTotal"] == PARTIAL and abs(after["remainingTotal"] - (payable - PARTIAL)) < 0.005, after
print("RESULT ok: paid", after["paidTotal"], "remaining", after["remainingTotal"], "message:", after["message"])
print("orderId:", draft["orderId"], "billId:", bill_id, "displayId:", display_id, "cashSessionId:", session_id)
